using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace BodyCheck.Core;

/// <summary>
/// An action in progress, which finishes once, or fails with an exception. Whatever ran it can <c>await</c> it, and an
/// <c>async</c> method can return one. Only Core makes and finishes them: an async method's, and an
/// <see cref="ITick"/>'s waits.
/// <para>
/// Nothing that awaits it runs when it finishes: what comes after each <c>await</c> is added to its
/// <see cref="ActionTaskQueue"/>, which the world runs at a fixed point in its tick. So finishing one from outside the
/// tick, such as from a Godot signal, only queues what waits for it. An <c>async</c> method that returns one can await
/// only <c>ActionTask</c>s, since anything else would resume outside the tick; awaiting anything else fails it.
/// </para>
/// <para>
/// Failing one fails the tick: the queue throws the exception when it next runs, whether or not anything awaits the
/// task, so that it is never lost. Only Godot's main thread may use one.
/// </para>
/// </summary>
[AsyncMethodBuilder(typeof(ActionTaskMethodBuilder))]
public sealed class ActionTask
{
    // Null only for an async method's task until its method first awaits a task that hasn't finished, whose queue it
    // then takes, and for Finished.
    private ActionTaskQueue? _queue;

    private readonly List<Action> _continuations = [];

    private ExceptionDispatchInfo? _failure;

    /// <param name="queue">The queue that runs what waits for the task.</param>
    internal ActionTask(ActionTaskQueue queue) => _queue = queue;

    private ActionTask()
    {
    }

    /// <summary>A task that has already finished, for an action that finishes at once.</summary>
    public static ActionTask Finished { get; } = new() { IsFinished = true };

    /// <summary>The task for an async method, which takes its queue from the first task its method awaits.</summary>
    internal static ActionTask ForAsyncMethod() => new();

    /// <summary>The queue of a task that hasn't finished, which every such task has.</summary>
    internal ActionTaskQueue Queue =>
        _queue ?? throw new UnreachableException("An unfinished async method's task has a queue.");

    /// <summary>Whether the task has finished or failed.</summary>
    public bool IsFinished { get; private set; }

    /// <summary>Finishes the task, queueing what waits for it. A task finishes only once.</summary>
    internal void Finish() => Settle(null);

    /// <summary>
    /// Fails the task, queueing the exception, which the queue throws when it next runs, then what waits for the task,
    /// which never runs once the exception has failed the tick. A task finishes only once.
    /// </summary>
    internal void Fail(Exception exception) => Settle(ExceptionDispatchInfo.Capture(exception));

    private void Settle(ExceptionDispatchInfo? failure)
    {
        if (IsFinished)
        {
            throw new InvalidOperationException("An ActionTask finishes only once.");
        }
        IsFinished = true;
        _failure = failure;

        if (_queue is null)
        {
            // An async method that has finished without waiting, so nothing waits for its task. If it failed, it fails
            // its caller, as a method that isn't async would.
            Debug.Assert(_continuations.Count == 0);
            failure?.Throw();
            return;
        }

        if (failure is not null)
        {
            _queue.Add(failure.Throw);
        }
        foreach (Action continuation in _continuations)
        {
            _queue.Add(continuation);
        }
        _continuations.Clear();
    }

    /// <summary>
    /// Makes <paramref name="queue"/> the queue of an async method's task, if it has none. One world's queue runs all of
    /// a task's chain, so a task given another fails.
    /// </summary>
    internal void TakeQueue(ActionTaskQueue queue)
    {
        _queue ??= queue;
        if (_queue != queue)
        {
            throw new InvalidOperationException("An async method awaited ActionTasks of two queues.");
        }
    }

    public Awaiter GetAwaiter() => new(this);

    /// <summary>What C#'s <c>await</c> uses to wait for an <see cref="ActionTask"/>.</summary>
    public readonly struct Awaiter : INotifyCompletion
    {
        internal Awaiter(ActionTask task) => Task = task;

        internal ActionTask Task { get; }

        public bool IsCompleted => Task.IsFinished;

        /// <summary>
        /// Queues <paramref name="continuation"/> for when the task finishes. C# calls this only for a task that hasn't.
        /// </summary>
        public void OnCompleted(Action continuation)
        {
            if (Task.IsFinished)
            {
                throw new InvalidOperationException("The ActionTask has already finished.");
            }
            Debug.Assert(Task._queue is not null, "An unfinished async method's task has a queue.");
            Task._continuations.Add(continuation);
        }

        /// <summary>Throws the task's exception if it failed.</summary>
        public void GetResult()
        {
            if (!Task.IsFinished)
            {
                throw new InvalidOperationException("The ActionTask hasn't finished.");
            }
            Task._failure?.Throw();
        }
    }
}
