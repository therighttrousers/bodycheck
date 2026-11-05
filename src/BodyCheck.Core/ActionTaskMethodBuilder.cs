using System.Runtime.CompilerServices;

namespace BodyCheck.Core;

/// <summary>
/// What the C# compiler uses to build an <see cref="ActionTask"/> for an <c>async</c> method that returns one. The
/// compiler turns the method into a state machine, and calls these methods from it: code shouldn't call them.
/// <para>
/// The method runs at once, up to its first <c>await</c> of a task that hasn't finished. Its task then takes that
/// task's queue, and the method resumes when that queue runs. Awaiting anything but an <see cref="ActionTask"/> fails
/// the method's task.
/// </para>
/// </summary>
public struct ActionTaskMethodBuilder
{
    private ActionTask? _task;

    // The state machine, boxed on its first await, if it is a struct, so that every resumption runs the same copy.
    private IAsyncStateMachine? _stateMachine;

    public static ActionTaskMethodBuilder Create() => default;

    public ActionTask Task => _task ??= ActionTask.ForAsyncMethod();

    public readonly void Start<TStateMachine>(ref TStateMachine stateMachine)
        where TStateMachine : IAsyncStateMachine =>
        stateMachine.MoveNext();

    /// <summary>Called on the boxed state machine's own builder, to tell it the box.</summary>
    public void SetStateMachine(IAsyncStateMachine stateMachine) => _stateMachine = stateMachine;

    public void SetResult() => Task.Finish();

    public void SetException(Exception exception) => Task.Fail(exception);

    public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
        where TAwaiter : INotifyCompletion
        where TStateMachine : IAsyncStateMachine
    {
        if (awaiter is not ActionTask.Awaiter actionAwaiter)
        {
            // The compiler calls this inside the method's try, so the exception fails the method's task.
            throw new InvalidOperationException(
                $"An async method that returns an ActionTask can await only ActionTasks, not {typeof(TAwaiter)}, " +
                "which would resume it outside the world's tick.");
        }

        // Before boxing, so that the box's builder and this one share the task, which the method's caller gets from
        // this one.
        Task.TakeQueue(actionAwaiter.Task.Queue);

        if (_stateMachine is null)
        {
            // In a build that makes the state machine a class, this is the same object, and SetStateMachine does nothing.
            IAsyncStateMachine box = stateMachine;
            box.SetStateMachine(box);
            _stateMachine = box;
        }
        actionAwaiter.OnCompleted(_stateMachine.MoveNext);
    }

    public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
        where TAwaiter : ICriticalNotifyCompletion
        where TStateMachine : IAsyncStateMachine =>
        AwaitOnCompleted(ref awaiter, ref stateMachine);
}
