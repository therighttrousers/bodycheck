# Pilot 1: rewritten comments and docstrings

Instead of rating the A-F outputs, I've rewritten each file's comments and docstrings. Typically, for each comment or docstring, I took the output I thought was clearest from A-F, and altered it.

I'm aware that I'm exhibiting changes that are substantially different from simply fixing awkward grammar. Reasons I'm doing this instead of rating:
- I'm finding it too cognitively demanding to try to give Likert scale ratings to whole files, where each file's clarity is mixed, and which have at most moderate substantial differences.
- I'm having difficulty separating grammar issues from more fundamental issues about audience, abstractions, expectations, contracts, etc. In particular, seeing descriptions written as a *big bag of stuff that happens* instead of the stuff being sorted into *requirements and provisions* overshadows everything else for me.
- I *feel like* if more fundamental issues were addressed, the grammar issues would be greatly reduced. This is how I would help a human student or junior engineer, because I would expect that keeping the abstraction boundaries and audience in mind would make *how to connect the ideas precisely* easier to get right. I don't know if that's true for LLMs, or if it is, how to teach them this in a way that simulates internalizing it.

## StallDetector.cs

Notes:
- First sentence of `StallTuning`'s docstring taken from B.
- No `StallTuning` docstring framed "functions instead of values" as part of a *contract that users of `StallDetector` should meet*. Without that framing, the docstring remains a weak description of what hopefully happens, regardless of its grammar. With that framing, the docstring can be put in a command form that clearly sets expectations. The framing requires regarding the file as an abstraction boundary. 
- Took "body" terminology from B and C. "Thing" is too general.
- Took `MinSpeed` docstring from B. Changed "slow" to "too slow."
- Took `MaxTime` docstring from C (not B only because "must" is more precise than "may" here). Changed "slow" to "too slow." Added positivity precondition.
- Took `StallDetector` docstring from B.
- Took `_slowTime` comment from F. I would probably rename this to `_stallTimer` given my further comment changes.
- `Reset` docstrings are all poor, but D's "slowness of turning" and F's "the time before" are especially unclear. I've written my own. Note that like the `StallTuning` docstring, it clearly sets expectations for use with command form.
- Took `Update` summary from B. Changed "slow time" to "stall timer." Changed "while" to "if" for precision - it shouldn't connote a loop. I realize that by using the term "stall timer," I've exposed an internal concept so that it becomes part of the user's mental model. There was already an implicit attempt at that with "slow time," but it was inconsistently done and didn't bring much to mind (for me).
- Took `Update` param `speed` docstring from C and altered it greatly.
- Took `Update` param `gameDt` docstring from C. Added "physics."
- Took `Update` return docstring from C.

The closest I can get to ratings for `StallDetector.cs` outputs: I like B and C better than the others, and C probably edges out B.

```csharp
namespace BodyCheck.Core.Controllers;

/// <summary>
/// The thresholds for a <see cref="StallDetector"/>. Supply functions that read threshold values from the current
/// features so the detector can always use up-to-date thresholds even if the feature configuration changes.
/// </summary>
public sealed record StallTuning
{
    /// <summary>The speed below which a body counts as too slow.</summary>
    public required Func<float> MinSpeed { get; init; }
    /// <summary>How long, in game seconds, a body must stay too slow to count as stalled. Should be positive.</summary>
    public required Func<float> MaxTime { get; init; }
}

/// <summary>
/// Detects a stall: a body that stays slower than <see cref="StallTuning.MinSpeed"/> for <see cref="StallTuning.MaxTime"/>.
/// </summary>
internal sealed class StallDetector(StallTuning tuning)
{
    // How long, in game time, the speed has stayed below the minimum without a break.
    private float _slowTime;

    /// Resets the stall timer. Call this if the body changes direction or stops trying to move.
    public void Reset() => _slowTime = 0;

    /// <summary>
    /// Adds <paramref name="gameDt"/> to the stall timer if <paramref name="speed"/> is below the minimum, and clears it otherwise.
    /// </summary>
    /// <param name="speed">The body's signed speed in the intended direction. It should be negative when velocity is opposite. Consider passing a scalar projection.</param>
    /// <param name="gameDt">The physics step's length in game time.</param>
    /// <returns>Whether the body has been slower than the minimum speed for at least the maximum time.</returns>
    public bool Update(float speed, float gameDt)
    {
        _slowTime = speed < tuning.MinSpeed() ? _slowTime + gameDt : 0;
        return _slowTime >= tuning.MaxTime();
    }
}
```

## ScopedTick.cs

Notes:
- Took `ScopedTick` summary from E for its "view" framing. Added "Use this" instruction.
- Took `NextTick` docstring from A. Except for B, the others inherited the docstring, which is a run-on infodump.
- Took `Wait` docstring from A, for the same reason. I recall the run-on infodump being the result of iterating on the design. It includes details that don't need to be communicated to users of `ScopedTick`, and if for some reason they ever really did need those details, they could more easily understand by reading the code. (At least, a human would.)
- Took `Scoped` docstring from A, for the same reason. Added "i.e." and a mention of nesting.
- Took `While` docstring from A, for the same reason. Added "(and the tick stays in scope)," which is important for human context window sizes (i.e. small).
- Ditto for `UntilCompleted`, `All`, `Dt`, `Time` - took A. It's the only output that doesn't inherit docstrings (implicitly or explicitly). Added mention of scope for the same reason as added in `While`.

A is the only output I can say I like due to the others inheriting poor docstrings.

```csharp
namespace BodyCheck.Core.Tasks;

/// <summary>
/// A view of the tick scheduler whose waits are cancelled when a condition stops holding. Use this to scope async tasks
/// based on validity; e.g. to cancel after a timeout or keep running only while dependencies are alive/valid.
/// </summary>
internal sealed class ScopedTick(TickScheduler scheduler, Func<bool>? condition) : ITick
{
    /// <summary>Waits until the next tick.</summary>
    public TickTask NextTick() => Wait(0);

    /// <summary>Waits the given number of game seconds.</summary>
    public TickTask Wait(float seconds) => scheduler.Wait(seconds, condition);

    /// <summary>
    /// Returns a tick like this one, but whose condition also requires <paramref name="inner"/> to hold; i.e.
    /// it returns a tick with a nested scope.
    /// </summary>
    public ITick Scoped(Func<bool> inner) =>
        new ScopedTick(scheduler, condition is null ? inner : () => condition() && inner());

    /// <summary>Waits, one tick at a time, while <paramref name="condition"/> holds (and the tick stays in scope).</summary>
    public async TickTask While(Func<bool> condition)
    {
        while (condition())
        {
            await NextTick();
        }
    }

    /// <summary>Runs <paramref name="body"/> each tick while <paramref name="condition"/> holds (and the tick stays in scope).</summary>
    public async TickTask While(Func<bool> condition, Action body)
    {
        while (condition())
        {
            body();
            await NextTick();
        }
    }

    /// <summary>
    /// Runs <paramref name="body"/> while <paramref name="condition"/> holds (and the tick stays in scope). If the body
    /// finishes without time passing, the loop waits one tick, so it can't spin forever in the same tick.
    /// </summary>
    public async TickTask While(Func<bool> condition, Func<TickTask> body)
    {
        while (condition())
        {
            double time = Time;
            await body();
            if (Time == time)
            {
                await NextTick();
            }
        }
    }

    /// <summary>
    /// Waits until <paramref name="task"/> completes (while the tick stays in scope), then rethrows its exception if it failed.
    /// </summary>
    public async TickTask UntilCompleted(Task task)
    {
        await While(() => !task.IsCompleted);
        task.GetAwaiter().GetResult();
    }

    /// <summary>Waits for each of <paramref name="tasks"/> in turn (as long as the tick stays in scope).</summary>
    public async TickTask All(IEnumerable<TickTask> tasks)
    {
        // Copy the tasks first, so the list can't change while they are awaited.
        List<TickTask> taskList = [.. tasks];
        foreach (var task in taskList)
        {
            await task;
        }
    }

    /// <summary>The length of the current tick, in game seconds.</summary>
    public float Dt => scheduler.Dt;

    /// <summary>The game time, in seconds.</summary>
    public double Time => scheduler.Time;
}
```

## Muffler.cs

Notes:
- Took `Muffler` docstring from C.
- Took `OpenCutoff` comment from F. Added engine limit from A.
- Took `SlowdownCurve` comment from F - first sentence only.
- Left out comments for `MasterBus` and other private members.
- Took `ProcessMode = ...` comment from E. Short, and no more needs to be said.
- Took `float muffled = ...` comment from E. Short and clear enough. More detail just below in the code for those interested.
- Took comments near the end of `_Process` from E. Gave clear reasons for the choices.

Apparently, I liked E best. 

```csharp
using BodyCheck.Core;

using Godot;

namespace BodyCheck.Modes;

/// <summary>
/// Muffles the game's sound when the game isn't being played, such as while it is paused or in a menu, and
/// gradually as it slows down for a phase.
/// </summary>
/// <remarks>
/// It muffles with the first low-pass filter on the master bus, which the audio bus layout must provide. The
/// filter's cutoff in the layout is the cutoff when fully muffled.
/// </remarks>
public partial class Muffler : Node
{
    // The cutoff, in hertz, at which the filter muffles nothing: just above human hearing, and the highest the engine allows.
    private const float OpenCutoff = 20500;

    // The power the slowdown's progress is raised to, for how muffled sound is while slowing for a phase.
    private const float SlowdownCurve = 0.5f;

    private const int MasterBus = 0;

    private readonly GameModeManager _gameModeManager;
    private AudioEffectLowPassFilter? _filter;
    private int _filterIndex;
    private float _closedCutoff;

    public Muffler(GameModeManager gameModeManager)
    {
        _gameModeManager = gameModeManager;
    }

    public override void _Ready()
    {
        // Keep muffling while the game is paused.
        ProcessMode = ProcessModeEnum.Always;
        for (int i = 0; i < AudioServer.GetBusEffectCount(MasterBus); i++)
        {
            if (AudioServer.GetBusEffect(MasterBus, i) is AudioEffectLowPassFilter filter)
            {
                _filter = filter;
                _filterIndex = i;
                _closedCutoff = filter.CutoffHz;
                return;
            }
        }
        GD.PushWarning("The master bus has no low-pass filter, so sound won't be muffled.");
    }

    public override void _Process(double delta)
    {
        if (_filter is null)
        {
            return;
        }

        // 0 is open and 1 is fully muffled. Slowing for a phase muffles in step with the slowdown.
        float muffling = _gameModeManager.GameMode switch
        {
            GameMode.Playing or GameMode.Cutscene => 0,
            GameMode.SlowingForPhase => Mathf.Pow((float)Mathf.Clamp(
                (1 - _gameModeManager.SlowdownFactor) / (1 - GameModeState.SlowestSlowdownFactor), 0, 1), SlowdownCurve),
            _ => 1,
        };
        // Disabled when open, so the filter costs nothing during play. The cutoff is interpolated by ratio, since pitch is logarithmic.
        AudioServer.SetBusEffectEnabled(MasterBus, _filterIndex, muffling > 0);
        _filter.CutoffHz = OpenCutoff * Mathf.Pow(_closedCutoff / OpenCutoff, muffling);
    }
}
```
