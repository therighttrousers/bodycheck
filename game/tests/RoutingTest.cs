using System.Collections.Generic;

using BodyCheck.Core;
using BodyCheck.Core.Entities;
using BodyCheck.Core.Scripts;
using BodyCheck.Core.Tasks;

using GdUnit4;

using static GdUnit4.Assertions;

namespace BodyCheck.Tests;

/// <summary>The runners of the control entities, on a scheduler of their own, with no world.</summary>
[TestSuite]
public class RoutingTest
{
    private const float Dt = 1f / 60;

    // gdUnit4 runs a suite's tests on one instance, so each test starts with a scheduler and a log of its own.
    private TickScheduler _scheduler = new();

    private readonly List<string> _log = [];

    [BeforeTest]
    public void Reset()
    {
        _scheduler = new TickScheduler();
        _log.Clear();
    }

    /// <summary>A target that logs when its action starts and ends, which takes some ticks.</summary>
    private sealed class Target(string name, int ticks, List<string> log) : IActionTarget
    {
        public async TickTask Default(ITick tick)
        {
            log.Add($"{name} starts");
            await tick.Wait(ticks * Dt);
            log.Add($"{name} ends");
        }
    }

    private Call CallOf(string name, int ticks) => new(new Target(name, ticks, _log), TriggerAction.Default);

    private void RunTicks(int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            _scheduler.Run(Dt);
        }
    }

    [TestCase]
    public void A_sequence_runs_each_step_when_the_one_before_has_finished()
    {
        var sequence = new SequenceRunner([CallOf("a", 2), CallOf("b", 1)]);

        TickTask run = sequence.Default(_scheduler);
        AssertThat(_log).ContainsExactly("a starts");

        RunTicks(2);
        AssertThat(_log).ContainsExactly("a starts", "a ends", "b starts");
        AssertThat(run.IsFinished).IsFalse();

        RunTicks(1);
        AssertThat(_log).ContainsExactly("a starts", "a ends", "b starts", "b ends");
        AssertThat(run.IsFinished).IsTrue();
    }

    [TestCase]
    public void A_sequence_runs_every_Default_alongside_any_in_progress()
    {
        var sequence = new SequenceRunner([CallOf("a", 2)]);

        sequence.Default(_scheduler);
        RunTicks(1);
        sequence.Default(_scheduler);
        AssertThat(_log).ContainsExactly("a starts", "a starts");
    }

    [TestCase]
    public void A_fanout_runs_its_steps_at_once_and_finishes_when_all_have()
    {
        var fanout = new FanoutRunner([CallOf("a", 2), CallOf("b", 1)]);

        TickTask run = fanout.Default(_scheduler);
        AssertThat(_log).ContainsExactly("a starts", "b starts");

        RunTicks(1);
        AssertThat(run.IsFinished).IsFalse();

        RunTicks(1);
        AssertThat(_log).ContainsExactly("a starts", "b starts", "b ends", "a ends");
        AssertThat(run.IsFinished).IsTrue();
    }

    [TestCase]
    public void A_trigger_touched_while_its_step_runs_starts_nothing()
    {
        var trigger = new TriggerRunner(CallOf("a", 2));

        TickTask first = trigger.Touch(_scheduler);
        RunTicks(1);
        AssertThat(trigger.Touch(_scheduler)).IsSame(first);
        AssertThat(_log).ContainsExactly("a starts");

        RunTicks(1);
        trigger.Touch(_scheduler);
        AssertThat(_log).ContainsExactly("a starts", "a ends", "a starts");
    }

    [TestCase]
    public void A_once_gives_every_Default_its_first_run()
    {
        var once = new OnceRunner(CallOf("a", 2));

        TickTask first = once.Default(_scheduler);
        RunTicks(1);
        // Asked while it runs, it finishes when the first run does.
        AssertThat(once.Default(_scheduler)).IsSame(first);

        RunTicks(1);
        // Asked after, it finishes at once, and runs nothing.
        TickTask after = once.Default(_scheduler);
        AssertThat(after.IsFinished).IsTrue();
        AssertThat(_log).ContainsExactly("a starts", "a ends");
    }
}
