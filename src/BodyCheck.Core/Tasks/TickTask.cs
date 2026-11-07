using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace BodyCheck.Core.Tasks;

/// <summary>
/// Work in progress that resumes only on a tick: it finishes once, fails with an exception, or is cancelled. It can be
/// awaited, and an <c>async</c> method can return one. Only its <see cref="TickScheduler"/> makes, finishes and
/// cancels tasks, for its waits, and the method builder, for async methods.
/// <para>
/// Nothing that awaits a task runs when it finishes: what comes after each <c>await</c> is added to its scheduler,
/// which runs it when its owner next runs the scheduler, once a tick. So finishing one between ticks only queues what
/// waits for it. An <c>async</c> method that returns one can await only <c>TickTask</c>s, since anything else would
/// resume it off the tick; awaiting anything else fails it.
/// </para>
/// <para>
/// Failing one throws its exception in what awaits it, as <c>await</c> does with any exception. A failed task that
/// nothing has awaited by the end of the scheduler's run in which it failed is unhandled: the scheduler gives its
/// exception to its owner's sink, so that it is never lost, and everything else carries on. Tasks and their scheduler
/// are for one thread.
/// </para>
/// <para>
/// Cancelling one isn't a failure, and is never reported: awaiting it throws an
/// <see cref="OperationCanceledException"/>, which unwinds what awaits it through its <c>using</c> and <c>finally</c>
/// blocks, and cancels that method's task in turn. Only the scheduler cancels tasks: when it stops, and when the scope
/// of a wait ends (<see cref="ITick.Scoped"/>).
/// </para>
/// </summary>
[AsyncMethodBuilder(typeof(TickTaskMethodBuilder))]
public sealed class TickTask
{
    // Null only for an async method's task until its method first awaits a task that hasn't finished, whose scheduler
    // it then takes, and for Finished.
    private TickScheduler? _scheduler;

    private readonly List<Action> _continuations = [];

    private ExceptionDispatchInfo? _failure;

    // Whether anything has awaited the task: attached a continuation, or read the result of the task once finished.
    private bool _awaited;

    /// <param name="scheduler">The scheduler that runs what waits for the task.</param>
    internal TickTask(TickScheduler scheduler) => _scheduler = scheduler;

    private TickTask()
    {
    }

    /// <summary>A task that has already finished, for work that finishes at once.</summary>
    public static TickTask Finished { get; } = new() { IsFinished = true };

    /// <summary>The task for an async method, which takes its scheduler from the first task its method awaits.</summary>
    internal static TickTask ForAsyncMethod() => new();

    /// <summary>The scheduler of a task that hasn't finished, which every such task has.</summary>
    internal TickScheduler Scheduler =>
        _scheduler ?? throw new UnreachableException("An unfinished async method's task has a scheduler.");

    /// <summary>Whether the task has finished, failed or been cancelled.</summary>
    public bool IsFinished { get; private set; }

    /// <summary>Whether the task has been cancelled.</summary>
    public bool IsCancelled { get; private set; }

    /// <summary>The exception of a task that has failed, not been cancelled, if nothing has awaited it.</summary>
    internal Exception? UnhandledFailure => _awaited || IsCancelled ? null : _failure?.SourceException;

    /// <summary>Finishes the task, queueing what waits for it. A task finishes only once.</summary>
    internal void Finish() => Settle(null);

    /// <summary>
    /// Cancels the task with <paramref name="exception"/>, which awaiting it throws, queueing what waits for it. Unlike
    /// a failure, it is never unhandled. A task finishes only once.
    /// </summary>
    internal void Cancel(OperationCanceledException exception)
    {
        IsCancelled = true;
        Settle(ExceptionDispatchInfo.Capture(exception));
    }

    /// <summary>
    /// Fails the task with <paramref name="exception"/>, which awaiting it throws, queueing what waits for it, and tells
    /// the scheduler, which reports it if nothing has awaited the task by the end of its run. A task finishes only once.
    /// </summary>
    internal void Fail(Exception exception) => Settle(ExceptionDispatchInfo.Capture(exception));

    private void Settle(ExceptionDispatchInfo? failure)
    {
        if (IsFinished)
        {
            throw new InvalidOperationException("A TickTask finishes only once.");
        }
        IsFinished = true;
        _failure = failure;

        if (_scheduler is null)
        {
            // An async method that has finished without waiting, so nothing waits for its task. If it failed or was
            // cancelled, it throws to its caller, as a method that isn't async would.
            Debug.Assert(_continuations.Count == 0);
            failure?.Throw();
            return;
        }

        if (failure is not null && !IsCancelled)
        {
            _scheduler.Failed(this);
        }
        foreach (Action continuation in _continuations)
        {
            _scheduler.Add(continuation);
        }
        _continuations.Clear();
    }

    /// <summary>
    /// Makes <paramref name="scheduler"/> the scheduler of an async method's task, if it has none. One scheduler runs
    /// all of a task's chain, so a task given another fails.
    /// </summary>
    internal void TakeScheduler(TickScheduler scheduler)
    {
        _scheduler ??= scheduler;
        if (_scheduler != scheduler)
        {
            throw new InvalidOperationException("An async method awaited TickTasks of two schedulers.");
        }
    }

    public Awaiter GetAwaiter() => new(this);

    /// <summary>What C#'s <c>await</c> uses to wait for a <see cref="TickTask"/>.</summary>
    public readonly struct Awaiter : INotifyCompletion
    {
        internal Awaiter(TickTask task) => Task = task;

        internal TickTask Task { get; }

        public bool IsCompleted => Task.IsFinished;

        /// <summary>
        /// Queues <paramref name="continuation"/> for when the task finishes. C# calls this only for a task that hasn't.
        /// </summary>
        public void OnCompleted(Action continuation)
        {
            if (Task.IsFinished)
            {
                throw new InvalidOperationException("The TickTask has already finished.");
            }
            Debug.Assert(Task._scheduler is not null, "An unfinished async method's task has a scheduler.");
            Task._awaited = true;
            Task._continuations.Add(continuation);
        }

        /// <summary>Throws the task's exception if it failed or was cancelled.</summary>
        public void GetResult()
        {
            if (!Task.IsFinished)
            {
                throw new InvalidOperationException("The TickTask hasn't finished.");
            }
            // Awaiting a task that has already finished reads its result without attaching a continuation.
            Task._awaited = true;
            Task._failure?.Throw();
        }
    }
}
