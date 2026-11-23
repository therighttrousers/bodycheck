using System;
using System.Collections.Generic;

using BodyCheck.Core;
using BodyCheck.Core.Entities;
using BodyCheck.Core.Scripts;
using BodyCheck.Core.Tasks;

using GdUnit4;

using static BodyCheck.Tests.Exceptions;
using static GdUnit4.Assertions;

namespace BodyCheck.Tests;

/// <summary>The runners of the control entities, on a scheduler of their own, with no world.</summary>
[TestSuite]
public class RoutingTest
{
    private const float Dt = 1f / 60;

    // gdUnit4 runs a suite's tests on one instance, so each test starts with a scheduler and a log of its own.
    private TickScheduler _scheduler = new(Rethrow);

    private readonly List<string> _log = [];

    [BeforeTest]
    public void Reset()
    {
        _scheduler = new TickScheduler(Rethrow);
        _log.Clear();
    }

    /// <summary>A target that logs when its action starts and ends, which takes some ticks.</summary>
    private sealed class Target(string name, int ticks, List<string> log) : IActionTarget
    {
        public async TickTask Run(ITick tick)
        {
            log.Add($"{name} starts");
            await tick.Wait(ticks * Dt);
            log.Add($"{name} ends");
        }
    }

    private Call CallOf(string name, int ticks) => new(new Target(name, ticks, _log), TriggerAction.Run);

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

        TickTask run = sequence.Run(_scheduler.Tick);
        AssertThat(_log).ContainsExactly("a starts");

        RunTicks(2);
        AssertThat(_log).ContainsExactly("a starts", "a ends", "b starts");
        AssertThat(run.IsFinished).IsFalse();

        RunTicks(1);
        AssertThat(_log).ContainsExactly("a starts", "a ends", "b starts", "b ends");
        AssertThat(run.IsFinished).IsTrue();
    }

    [TestCase]
    public void A_sequence_runs_every_Run_alongside_any_in_progress()
    {
        var sequence = new SequenceRunner([CallOf("a", 2)]);

        sequence.Run(_scheduler.Tick);
        RunTicks(1);
        sequence.Run(_scheduler.Tick);
        AssertThat(_log).ContainsExactly("a starts", "a starts");
    }

    [TestCase]
    public void A_fanout_runs_its_steps_at_once_and_finishes_when_all_have()
    {
        var fanout = new FanoutRunner([CallOf("a", 2), CallOf("b", 1)]);

        TickTask run = fanout.Run(_scheduler.Tick);
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
        var trigger = new TriggerRunner(CallOf("a", 2), SwitchOf(true));

        TickTask first = trigger.SetOff(_scheduler.Tick);
        RunTicks(1);
        AssertThat(trigger.SetOff(_scheduler.Tick)).IsSame(first);
        AssertThat(_log).ContainsExactly("a starts");

        RunTicks(1);
        trigger.SetOff(_scheduler.Tick);
        AssertThat(_log).ContainsExactly("a starts", "a ends", "a starts");
    }

    [TestCase]
    public void A_trigger_that_is_off_starts_nothing_until_turned_on()
    {
        var trigger = new TriggerRunner(CallOf("a", 1), SwitchOf(false));

        AssertThat(trigger.SetOff(_scheduler.Tick).IsFinished).IsTrue();
        AssertThat(_log).IsEmpty();

        AssertThat(trigger.TurnOn(_scheduler.Tick).IsFinished).IsTrue();
        trigger.SetOff(_scheduler.Tick);
        AssertThat(_log).ContainsExactly("a starts");
    }

    [TestCase]
    public void A_once_turns_off_when_its_step_finishes_and_runs_again_once_turned_on()
    {
        var state = new Switch { IsOn = true };
        var once = new OnceRunner(CallOf("a", 2), () => state);

        TickTask first = once.Run(_scheduler.Tick);
        RunTicks(1);
        // Asked while it runs, it finishes when the first run does, and it is still on until then.
        AssertThat(once.Run(_scheduler.Tick)).IsSame(first);
        AssertThat(state.IsOn).IsTrue();

        RunTicks(1);
        // Asked after, it finishes at once, and runs nothing.
        AssertThat(state.IsOn).IsFalse();
        AssertThat(once.Run(_scheduler.Tick).IsFinished).IsTrue();
        AssertThat(_log).ContainsExactly("a starts", "a ends");

        // Turned on again, it runs again.
        once.TurnOn(_scheduler.Tick);
        once.Run(_scheduler.Tick);
        AssertThat(_log).ContainsExactly("a starts", "a ends", "a starts");
    }

    /// <summary>A trigger's or a once's switch, as the game's state would hold it.</summary>
    private sealed class Switch : IGameState.ISwitchState
    {
        public bool IsOn { get; set; }
    }

    private static Func<IGameState.ISwitchState> SwitchOf(bool isOn)
    {
        var state = new Switch { IsOn = isOn };
        return () => state;
    }
}
