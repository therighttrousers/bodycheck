using System.Runtime.CompilerServices;

namespace BodyCheck.Core.Tasks;

/// <summary>
/// What the C# compiler uses to build a <see cref="TickTask"/> for an <c>async</c> method that returns one. The
/// compiler turns the method into a state machine, and calls these methods from it: code shouldn't call them.
/// <para>
/// The method runs at once, up to its first <c>await</c> of a task that hasn't finished. Its task then takes that
/// task's scheduler, and the method resumes when that scheduler runs. Awaiting anything but a <see cref="TickTask"/>
/// fails the method's task. A method that throws an <see cref="OperationCanceledException"/>, such as by awaiting a
/// cancelled task, cancels its task.
/// </para>
/// </summary>
public struct TickTaskMethodBuilder
{
    private TickTask? _task;

    // The state machine, boxed on its first await, if it is a struct, so that every resumption runs the same copy.
    private IAsyncStateMachine? _stateMachine;

    public static TickTaskMethodBuilder Create() => default;

    public TickTask Task => _task ??= TickTask.ForAsyncMethod();

    public readonly void Start<TStateMachine>(ref TStateMachine stateMachine)
        where TStateMachine : IAsyncStateMachine =>
        stateMachine.MoveNext();

    /// <summary>Called on the boxed state machine's own builder, to tell it the box.</summary>
    public void SetStateMachine(IAsyncStateMachine stateMachine) => _stateMachine = stateMachine;

    public void SetResult() => Task.Finish();

    /// <summary>
    /// Ends the method's task with the exception its method threw: cancelled, if it is a cancellation, such as from
    /// awaiting a cancelled task, so that a cancellation unwinding a chain doesn't fail the tick; otherwise failed.
    /// </summary>
    public void SetException(Exception exception)
    {
        if (exception is OperationCanceledException cancellation)
        {
            Task.Cancel(cancellation);
        }
        else
        {
            Task.Fail(exception);
        }
    }

    public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
        where TAwaiter : INotifyCompletion
        where TStateMachine : IAsyncStateMachine
    {
        if (awaiter is not TickTask.Awaiter tickAwaiter)
        {
            // The compiler calls this inside the method's try, so the exception fails the method's task.
            throw new InvalidOperationException(
                $"An async method that returns a TickTask can await only TickTasks, not {typeof(TAwaiter)}, " +
                "which would resume it off the tick.");
        }

        // Before boxing, so that the box's builder and this one share the task, which the method's caller gets from
        // this one.
        Task.TakeScheduler(tickAwaiter.Task.Scheduler);

        if (_stateMachine is null)
        {
            // In a build that makes the state machine a class, this is the same object, and SetStateMachine does nothing.
            IAsyncStateMachine box = stateMachine;
            box.SetStateMachine(box);
            _stateMachine = box;
        }
        tickAwaiter.OnCompleted(_stateMachine.MoveNext);
    }

    public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
        where TAwaiter : ICriticalNotifyCompletion
        where TStateMachine : IAsyncStateMachine =>
        AwaitOnCompleted(ref awaiter, ref stateMachine);
}
