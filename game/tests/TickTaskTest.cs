using System;
using System.Collections.Generic;
using System.Linq;
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
        var scheduler = new TickScheduler(Rethrow);
        var log = new List<string>();

        async TickTask Steps(ITick tick)
        {
            log.Add("start");
            await TickTask.Finished;
            log.Add("finished");
            await tick.NextTick();
            log.Add("next tick");
        }

        TickTask steps = Steps(scheduler.Tick);
        AssertThat(log).ContainsExactly("start", "finished");

        scheduler.Run(Dt);
        AssertThat(log).ContainsExactly("start", "finished", "next tick");
        AssertThat(steps.IsFinished).IsTrue();
    }

    [TestCase]
    public void Runs_a_chain_that_finishes_at_once_in_one_tick()
    {
        var scheduler = new TickScheduler(Rethrow);
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
        TickTask outer = Outer(scheduler.Tick);
        inner = Inner(scheduler.Tick);

        scheduler.Run(Dt);
        AssertThat(log).ContainsExactly("outer", "inner", "after inner");
        AssertThat(outer.IsFinished).IsTrue();
    }

    [TestCase]
    public void Waiting_for_no_time_finishes_in_the_next_tick()
    {
        var scheduler = new TickScheduler(Rethrow);
        int ticks = 0;

        async TickTask Count(ITick tick)
        {
            while (true)
            {
                await tick.Wait(0);
                ticks++;
            }
        }
        _ = Count(scheduler.Tick);

        scheduler.Run(Dt);
        scheduler.Run(Dt);
        scheduler.Run(Dt);
        AssertThat(ticks).IsEqual(3);
    }

    [TestCase]
    public void A_wait_made_before_the_scheduler_runs_finishes_in_that_run()
    {
        var scheduler = new TickScheduler(Rethrow);
        scheduler.Run(Dt);

        // As in a controller's Apply, before the world runs the scheduler in the same tick.
        TickTask wait = scheduler.Tick.NextTick();
        scheduler.Run(Dt);
        AssertThat(wait.IsFinished).IsTrue();
    }

    [TestCase(1f, 1f / 60, 60)]
    [TestCase(1f, 0.5f / 60, 120)]
    [TestCase(0.1f, 1f / 60, 6)]
    public void Waits_on_game_time_counted_in_played_ticks(float seconds, float gameDt, int ticks)
    {
        var scheduler = new TickScheduler(Rethrow);
        TickTask wait = scheduler.Tick.Wait(seconds);

        for (int i = 1; i < ticks; i++)
        {
            scheduler.Run(gameDt);
        }
        AssertThat(wait.IsFinished).IsFalse();
        scheduler.Run(gameDt);
        AssertThat(wait.IsFinished).IsTrue();
    }

    [TestCase]
    public void A_failure_goes_to_what_awaits_it_then_once_to_the_sink_while_the_rest_carries_on()
    {
        var failures = new List<Exception>();
        var scheduler = new TickScheduler(failures.Add);
        var log = new List<string>();

        static async TickTask Throws(ITick tick)
        {
            await tick.NextTick();
            throw new InvalidOperationException("broken");
        }

        async TickTask Awaits(ITick tick)
        {
            try
            {
                await Throws(tick);
            }
            finally
            {
                log.Add("awaiter cleaned up");
            }
        }

        async TickTask Unrelated(ITick tick)
        {
            await tick.NextTick();
            log.Add("unrelated resumed");
        }

        // Both resume in the same run: the failure doesn't stop the unrelated task.
        TickTask awaits = Awaits(scheduler.Tick);
        _ = Unrelated(scheduler.Tick);

        scheduler.Run(Dt);
        AssertThat(log).ContainsExactlyInAnyOrder("awaiter cleaned up", "unrelated resumed");
        AssertThat(awaits.IsFinished).IsTrue();
        AssertThat(awaits.IsCancelled).IsFalse();
        // Reported once, at the top of its chain, which nothing awaits.
        AssertThat(failures).HasSize(1);
        AssertThat(failures[0].Message).IsEqual("broken");
    }

    [TestCase]
    public void While_with_an_async_body_runs_it_once_a_tick_whether_or_not_it_lets_time_pass()
    {
        var scheduler = new TickScheduler(Rethrow);
        int instant = 0;
        int waiting = 0;

        // One body lets no time pass, so the loop waits for the next tick; the other waits for it itself, so the loop
        // doesn't wait again.
        _ = scheduler.Tick.While(() => true, async () =>
        {
            instant++;
            await TickTask.Finished;
        });
        _ = scheduler.Tick.While(() => true, async () =>
        {
            waiting++;
            await scheduler.Tick.NextTick();
        });
        AssertThat(instant).IsEqual(1);
        AssertThat(waiting).IsEqual(1);

        scheduler.Run(Dt);
        scheduler.Run(Dt);
        scheduler.Run(Dt);
        AssertThat(instant).IsEqual(4);
        AssertThat(waiting).IsEqual(4);
    }

    [TestCase]
    public void All_starts_every_task_of_a_lazy_sequence_before_awaiting_any()
    {
        var scheduler = new TickScheduler(Rethrow);
        var log = new List<string>();

        async TickTask Work(string name)
        {
            log.Add($"{name} starts");
            await scheduler.Tick.NextTick();
            log.Add($"{name} ends");
        }

        TickTask all = scheduler.Tick.All(new[] { "a", "b" }.Select(Work));
        AssertThat(log).ContainsExactly("a starts", "b starts");

        scheduler.Run(Dt);
        AssertThat(log).ContainsExactly("a starts", "b starts", "a ends", "b ends");
        AssertThat(all.IsFinished).IsTrue();
    }

    [TestCase]
    public void A_dotnet_task_completed_off_the_tick_resumes_what_waits_when_the_scheduler_next_runs()
    {
        var scheduler = new TickScheduler(Rethrow);
        var source = new TaskCompletionSource<int>();
        int result = 0;

        async TickTask Waits(ITick tick)
        {
            await tick.UntilCompleted(source.Task);
            result = source.Task.Result;
        }
        _ = Waits(scheduler.Tick);

        scheduler.Run(Dt);
        AssertThat(result).IsEqual(0);
        source.SetResult(1);
        AssertThat(result).IsEqual(0);
        scheduler.Run(Dt);
        AssertThat(result).IsEqual(1);
    }

    [TestCase]
    public void Stopping_unwinds_what_waits_through_its_finally_blocks_without_failing()
    {
        var scheduler = new TickScheduler(Rethrow);
        var log = new List<string>();

        async TickTask Inner(ITick tick)
        {
            try
            {
                await tick.Wait(10);
                log.Add("inner resumed");
            }
            finally
            {
                log.Add("inner cleaned up");
            }
        }

        async TickTask Outer(ITick tick)
        {
            using var cleanup = new Cleanup(() => log.Add("outer cleaned up"));
            await Inner(tick);
            log.Add("outer resumed");
        }

        TickTask outer = Outer(scheduler.Tick);
        scheduler.Run(Dt);

        scheduler.Stop();
        AssertThat(log).ContainsExactly("inner cleaned up", "outer cleaned up");
        AssertThat(outer.IsCancelled).IsTrue();
        AssertThat(scheduler.Tick.NextTick().IsCancelled).IsTrue();
    }

    [TestCase]
    public void Stopping_unwinds_everything_and_reports_a_cleanup_that_fails()
    {
        var failures = new List<Exception>();
        var scheduler = new TickScheduler(failures.Add);
        bool cleanedUp = false;

        static async TickTask FailsToCleanUp(ITick tick)
        {
            try
            {
                await tick.NextTick();
            }
            finally
            {
                throw new InvalidOperationException("broken");
            }
        }

        async TickTask CleansUp(ITick tick)
        {
            try
            {
                await tick.NextTick();
            }
            finally
            {
                cleanedUp = true;
            }
        }

        _ = FailsToCleanUp(scheduler.Tick);
        _ = CleansUp(scheduler.Tick);

        scheduler.Stop();
        AssertThat(cleanedUp).IsTrue();
        AssertThat(failures).HasSize(1);
        AssertThat(failures[0].Message).IsEqual("broken");
    }

    [TestCase]
    public void A_scope_that_ends_unwinds_what_waits_on_it_without_failing()
    {
        var scheduler = new TickScheduler(Rethrow);
        ITick world = scheduler.Tick;
        var log = new List<string>();

        async TickTask Loops(ITick tick, string name)
        {
            try
            {
                await tick.While(() => true, () => log.Add(name));
            }
            finally
            {
                log.Add($"{name} cleaned up");
            }
        }

        // A timeout: the scope lasts until the deadline, two ticks from now.
        double deadline = world.Time + 2 * Dt;
        TickTask scoped = Loops(world.Scoped(() => world.Time < deadline), "scoped");
        _ = Loops(world, "unscoped");

        scheduler.Run(Dt);
        scheduler.Run(Dt);
        // Each loop runs its body once at once, and once in the first tick; in the second, the scoped one ends.
        AssertThat(log).ContainsExactly(
            "scoped", "unscoped", "scoped", "unscoped", "unscoped", "scoped cleaned up");
        AssertThat(scoped.IsCancelled).IsTrue();
    }

    [TestCase]
    public void A_wait_in_an_ended_scope_is_cancelled_at_once_and_the_outer_scope_guards_the_inner()
    {
        var scheduler = new TickScheduler(Rethrow);
        bool there = false;
        bool innerChecked = false;

        ITick tick = scheduler.Tick
            .Scoped(() => there)
            .Scoped(() => innerChecked = true);

        AssertThat(tick.NextTick().IsCancelled).IsTrue();
        AssertThat(innerChecked).IsFalse();
    }

    [TestCase]
    public void A_condition_that_throws_ends_its_scope_and_goes_to_the_sink_once()
    {
        var failures = new List<Exception>();
        var scheduler = new TickScheduler(failures.Add);
        bool broken = false;
        ITick tick = scheduler.Tick.Scoped(() => broken ? throw new InvalidOperationException("broken") : true);

        // Two waits on one scope, so that its condition is checked, and throws, twice in the run.
        TickTask first = tick.Wait(1);
        TickTask second = tick.Wait(1);
        broken = true;

        scheduler.Run(Dt);
        AssertThat(first.IsCancelled).IsTrue();
        AssertThat(second.IsCancelled).IsTrue();
        AssertThat(failures).HasSize(1);
        AssertThat(failures[0].Message).IsEqual("broken");
    }

    [TestCase]
    public void What_a_scope_cuts_short_with_nothing_awaiting_it_goes_to_the_warning_sink()
    {
        var cutShort = new List<OperationCanceledException>();
        var scheduler = new TickScheduler(Rethrow, cutShort.Add);
        bool holds = true;
        ITick tick = scheduler.Tick.Scoped(() => holds);

        async TickTask Waits(ITick on) => await on.Wait(1);

        async TickTask Awaits(TickTask task) => await task;

        _ = Awaits(Waits(tick));
        TickTask stopped = Waits(scheduler.Tick);
        holds = false;
        scheduler.Run(Dt);
        // Only the top of the first chain, and not the second, whose scope holds.
        AssertThat(cutShort).HasSize(1);

        scheduler.Stop();
        AssertThat(stopped.IsCancelled).IsTrue();
        AssertThat(cutShort).HasSize(1);
    }

    [TestCase]
    public void Ending_scopes_cancels_what_ended_at_once_and_finishes_no_wait()
    {
        var scheduler = new TickScheduler(Rethrow);
        bool holds = true;
        var log = new List<string>();

        async TickTask Cleans(ITick tick)
        {
            try
            {
                await tick.NextTick();
            }
            finally
            {
                log.Add("cleaned up");
            }
        }

        TickTask scoped = Cleans(scheduler.Tick.Scoped(() => holds));
        TickTask unscoped = scheduler.Tick.NextTick();
        holds = false;
        scheduler.EndScopes();
        AssertThat(scoped.IsCancelled).IsTrue();
        AssertThat(log).ContainsExactly("cleaned up");
        AssertThat(unscoped.IsFinished).IsFalse();
    }

    private sealed class Cleanup(Action action) : IDisposable
    {
        public void Dispose() => action();
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
        var scheduler = new TickScheduler(Rethrow);
        var log = new List<string>();
        var queue = new ReentrantTask(Reentry.Queue);

        async TickTask Work(string name)
        {
            log.Add($"{name} starts");
            await scheduler.Tick.NextTick();
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
        var scheduler = new TickScheduler(Rethrow);
        var reentrant = new ReentrantTask(reentry);

        async TickTask Inner()
        {
            await scheduler.Tick.NextTick();
        }

        var e = Thrown<InvalidOperationException>(() => reentrant.Run(() => reentrant.Run(Inner)));
        AssertThat(e.Message).StartsWith("A ReentrantTask was asked for a run while starting one");
    }

    [TestCase]
    public void A_queued_request_made_while_starting_its_run_fails()
    {
        var failures = new List<Exception>();
        var scheduler = new TickScheduler(failures.Add);
        var queue = new ReentrantTask(Reentry.Queue);

        async TickTask Work()
        {
            await scheduler.Tick.NextTick();
        }

        _ = queue.Run(Work);
        // Queued behind the first, it asks for more of itself when it starts, in the scheduler's run.
        _ = queue.Run(() => queue.Run(Work));

        scheduler.Run(Dt);
        AssertThat(failures).HasSize(1);
        AssertThat(failures[0].Message).StartsWith("A ReentrantTask was asked for a run while starting one");
    }
}
