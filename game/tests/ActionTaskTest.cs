using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using BodyCheck.Core;

using GdUnit4;

using static BodyCheck.Tests.Exceptions;
using static GdUnit4.Assertions;

namespace BodyCheck.Tests;

[TestSuite]
public class ActionTaskTest
{
    private const float Dt = 1f / 60;

    [TestCase]
    public void Resumes_only_when_the_queue_runs()
    {
        var queue = new ActionTaskQueue();
        var log = new List<string>();

        async ActionTask Steps(ITick tick)
        {
            log.Add("start");
            await ActionTask.Finished;
            log.Add("finished");
            await tick.NextTick();
            log.Add("next tick");
        }

        ActionTask steps = Steps(queue);
        AssertThat(log).ContainsExactly("start", "finished");

        queue.Run(Dt);
        AssertThat(log).ContainsExactly("start", "finished", "next tick");
        AssertThat(steps.IsFinished).IsTrue();
    }

    [TestCase]
    public void Runs_a_chain_that_finishes_at_once_in_one_tick()
    {
        var queue = new ActionTaskQueue();
        var log = new List<string>();
        ActionTask? inner = null;

        async ActionTask Outer(ITick tick)
        {
            await tick.NextTick();
            log.Add("outer");
            // The inner task hasn't finished yet: it finishes later in the same run of the queue.
            await inner!;
            log.Add("after inner");
        }

        async ActionTask Inner(ITick tick)
        {
            await tick.NextTick();
            log.Add("inner");
        }

        // The outer method's wait is made first, so it resumes first.
        ActionTask outer = Outer(queue);
        inner = Inner(queue);

        queue.Run(Dt);
        AssertThat(log).ContainsExactly("outer", "inner", "after inner");
        AssertThat(outer.IsFinished).IsTrue();
    }

    [TestCase]
    public void Waiting_for_no_time_finishes_in_the_next_tick()
    {
        var queue = new ActionTaskQueue();
        int ticks = 0;

        async ActionTask Count(ITick tick)
        {
            while (true)
            {
                await tick.Wait(0);
                ticks++;
            }
        }
        _ = Count(queue);

        queue.Run(Dt);
        queue.Run(Dt);
        queue.Run(Dt);
        AssertThat(ticks).IsEqual(3);
    }

    [TestCase(1f, 1f / 60, 60)]
    [TestCase(1f, 0.5f / 60, 120)]
    [TestCase(0.1f, 1f / 60, 6)]
    public void Waits_on_game_time_counted_in_played_ticks(float seconds, float gameDt, int ticks)
    {
        var queue = new ActionTaskQueue();
        ActionTask wait = queue.Wait(seconds);

        for (int i = 1; i < ticks; i++)
        {
            queue.Run(gameDt);
        }
        AssertThat(wait.IsFinished).IsFalse();
        queue.Run(gameDt);
        AssertThat(wait.IsFinished).IsTrue();
    }

    [TestCase]
    public void An_exception_fails_the_tick_even_if_nothing_awaits_it()
    {
        var queue = new ActionTaskQueue();

        static async ActionTask Throws(ITick tick)
        {
            await tick.NextTick();
            throw new InvalidOperationException("broken");
        }
        _ = Throws(queue);

        var e = Thrown<InvalidOperationException>(() => queue.Run(Dt));
        AssertThat(e.Message).IsEqual("broken");
    }

    [TestCase]
    public void Awaiting_anything_else_fails()
    {
        static async ActionTask AwaitsATask() => await Task.Yield();

        var e = Thrown<InvalidOperationException>(() => { _ = AwaitsATask(); });
        AssertThat(e.Message).StartsWith("An async method that returns an ActionTask can await only ActionTasks");
    }
}
