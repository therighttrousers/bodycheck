using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using BodyCheck.Core.Tasks;

using GdUnit4;

using static BodyCheck.Tests.Exceptions;
using static GdUnit4.Assertions;

namespace BodyCheck.Tests;

[TestSuite]
public class TickTaskTest
{
    private const float Dt = 1f / 60;

    [TestCase]
    public void Resumes_only_when_the_scheduler_runs()
    {
        var scheduler = new TickScheduler();
        var log = new List<string>();

        async TickTask Steps(ITick tick)
        {
            log.Add("start");
            await TickTask.Finished;
            log.Add("finished");
            await tick.NextTick();
            log.Add("next tick");
        }

        TickTask steps = Steps(scheduler);
        AssertThat(log).ContainsExactly("start", "finished");

        scheduler.Run(Dt);
        AssertThat(log).ContainsExactly("start", "finished", "next tick");
        AssertThat(steps.IsFinished).IsTrue();
    }

    [TestCase]
    public void Runs_a_chain_that_finishes_at_once_in_one_tick()
    {
        var scheduler = new TickScheduler();
        var log = new List<string>();
        TickTask? inner = null;

        async TickTask Outer(ITick tick)
        {
            await tick.NextTick();
            log.Add("outer");
            // The inner task hasn't finished yet: it finishes later in the same run of the scheduler.
            await inner!;
            log.Add("after inner");
        }

        async TickTask Inner(ITick tick)
        {
            await tick.NextTick();
            log.Add("inner");
        }

        // The outer method's wait is made first, so it resumes first.
        TickTask outer = Outer(scheduler);
        inner = Inner(scheduler);

        scheduler.Run(Dt);
        AssertThat(log).ContainsExactly("outer", "inner", "after inner");
        AssertThat(outer.IsFinished).IsTrue();
    }

    [TestCase]
    public void Waiting_for_no_time_finishes_in_the_next_tick()
    {
        var scheduler = new TickScheduler();
        int ticks = 0;

        async TickTask Count(ITick tick)
        {
            while (true)
            {
                await tick.Wait(0);
                ticks++;
            }
        }
        _ = Count(scheduler);

        scheduler.Run(Dt);
        scheduler.Run(Dt);
        scheduler.Run(Dt);
        AssertThat(ticks).IsEqual(3);
    }

    [TestCase]
    public void A_wait_made_before_the_scheduler_runs_finishes_in_that_run()
    {
        var scheduler = new TickScheduler();
        scheduler.Run(Dt);

        // As in a controller's Apply, before the world runs the scheduler in the same tick.
        TickTask wait = scheduler.NextTick();
        scheduler.Run(Dt);
        AssertThat(wait.IsFinished).IsTrue();
    }

    [TestCase(1f, 1f / 60, 60)]
    [TestCase(1f, 0.5f / 60, 120)]
    [TestCase(0.1f, 1f / 60, 6)]
    public void Waits_on_game_time_counted_in_played_ticks(float seconds, float gameDt, int ticks)
    {
        var scheduler = new TickScheduler();
        TickTask wait = scheduler.Wait(seconds);

        for (int i = 1; i < ticks; i++)
        {
            scheduler.Run(gameDt);
        }
        AssertThat(wait.IsFinished).IsFalse();
        scheduler.Run(gameDt);
        AssertThat(wait.IsFinished).IsTrue();
    }

    [TestCase]
    public void An_exception_fails_the_tick_even_if_nothing_awaits_it()
    {
        var scheduler = new TickScheduler();

        static async TickTask Throws(ITick tick)
        {
            await tick.NextTick();
            throw new InvalidOperationException("broken");
        }
        _ = Throws(scheduler);

        var e = Thrown<InvalidOperationException>(() => scheduler.Run(Dt));
        AssertThat(e.Message).IsEqual("broken");
    }

    [TestCase]
    public void A_completion_finished_off_the_tick_resumes_what_waits_when_the_scheduler_next_runs()
    {
        var scheduler = new TickScheduler();
        TickCompletion completion = scheduler.NewCompletion();
        bool resumed = false;

        async TickTask Waits()
        {
            await completion.Task;
            resumed = true;
        }
        _ = Waits();

        scheduler.Run(Dt);
        AssertThat(resumed).IsFalse();
        completion.Finish();
        AssertThat(resumed).IsFalse();
        scheduler.Run(Dt);
        AssertThat(resumed).IsTrue();
    }

    [TestCase]
    public void Awaiting_anything_else_fails()
    {
        static async TickTask AwaitsATask() => await Task.Yield();

        var e = Thrown<InvalidOperationException>(() => { _ = AwaitsATask(); });
        AssertThat(e.Message).StartsWith("An async method that returns a TickTask can await only TickTasks");
    }

    [TestCase]
    public void A_queued_request_runs_once_the_last_asked_for_has_finished()
    {
        var scheduler = new TickScheduler();
        var log = new List<string>();
        var queue = new ReentrantTask(Reentry.Queue);

        async TickTask Work(string name)
        {
            log.Add($"{name} starts");
            await scheduler.NextTick();
            log.Add($"{name} ends");
        }

        _ = queue.Run(() => Work("a"));
        _ = queue.Run(() => Work("b"));
        TickTask c = queue.Run(() => Work("c"));
        AssertThat(log).ContainsExactly("a starts");

        scheduler.Run(Dt);
        AssertThat(log).ContainsExactly("a starts", "a ends", "b starts");
        scheduler.Run(Dt);
        scheduler.Run(Dt);
        AssertThat(log).ContainsExactly("a starts", "a ends", "b starts", "b ends", "c starts", "c ends");
        AssertThat(c.IsFinished).IsTrue();
    }

    [TestCase(Reentry.Join)]
    [TestCase(Reentry.Once)]
    [TestCase(Reentry.Queue)]
    public void A_request_made_while_starting_a_run_throws_even_with_none_in_progress(Reentry reentry)
    {
        var scheduler = new TickScheduler();
        var reentrant = new ReentrantTask(reentry);

        async TickTask Inner()
        {
            await scheduler.NextTick();
        }

        var e = Thrown<InvalidOperationException>(() => reentrant.Run(() => reentrant.Run(Inner)));
        AssertThat(e.Message).StartsWith("A ReentrantTask was asked for a run while starting one");
    }

    [TestCase]
    public void A_queued_request_made_while_starting_its_run_fails_the_tick()
    {
        var scheduler = new TickScheduler();
        var queue = new ReentrantTask(Reentry.Queue);

        async TickTask Work()
        {
            await scheduler.NextTick();
        }

        _ = queue.Run(Work);
        // Queued behind the first, it asks for more of itself when it starts, in the scheduler's run.
        _ = queue.Run(() => queue.Run(Work));

        var e = Thrown<InvalidOperationException>(() => scheduler.Run(Dt));
        AssertThat(e.Message).StartsWith("A ReentrantTask was asked for a run while starting one");
    }
}
