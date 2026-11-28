# Pilot 1: blind rating sheet

Each file appears in six versions, in a shuffled order. The code is identical in all of them; only the comments differ. For each version, rate how easily a reader new to the code understands the comments, from 1 (hard) to 5 (easy), and note anything that reads oddly. The key is in `rating-key.md`.

## StallDetector.cs

### StallDetector.cs, version A

Rating: 

```csharp
namespace BodyCheck.Core.Controllers;

/// <summary>The thresholds that decide when something counts as stalled. They are read each update, so they follow live tuning.</summary>
public sealed record StallTuning
{
    /// <summary>The speed below which the thing is considered to be making no progress.</summary>
    public required Func<float> MinSpeed { get; init; }
    /// <summary>How many seconds of game time it may stay below <see cref="MinSpeed"/> before it is stalled.</summary>
    public required Func<float> MaxTime { get; init; }
}

/// <summary>Detects that something has been moving too slowly, in the direction it wants to go, for too long.</summary>
internal sealed class StallDetector(StallTuning tuning)
{
    private float _slowTime;

    /// <summary>Forgets the time spent slow, such as after the thing turns around or stops trying to move.</summary>
    public void Reset() => _slowTime = 0;

    /// <summary>Counts <paramref name="gameDt"/> as slow time if <paramref name="speed"/> is below the minimum, and otherwise restarts the count.</summary>
    /// <param name="speed">The speed in the intended direction, so it is negative when pushed backwards.</param>
    /// <param name="gameDt">The game time that has passed, so a slowed game doesn't stall things.</param>
    /// <returns>Whether it has now been slow for the maximum time.</returns>
    public bool Update(float speed, float gameDt)
    {
        _slowTime = speed < tuning.MinSpeed() ? _slowTime + gameDt : 0;
        return _slowTime >= tuning.MaxTime();
    }
}
```

### StallDetector.cs, version B

Rating: 

```csharp
namespace BodyCheck.Core.Controllers;

/// <summary>
/// The thresholds for a <see cref="StallDetector"/>. They are functions, read each time they are needed, so they can change while the game runs.
/// </summary>
public sealed record StallTuning
{
    /// <summary>The speed below which a body counts as slow.</summary>
    public required Func<float> MinSpeed { get; init; }

    /// <summary>How long, in game seconds, a body may stay slow before it counts as stalled.</summary>
    public required Func<float> MaxTime { get; init; }
}

/// <summary>
/// Detects a stall: a body that stays slower than <see cref="StallTuning.MinSpeed"/> for <see cref="StallTuning.MaxTime"/>.
/// </summary>
internal sealed class StallDetector(StallTuning tuning)
{
    // Game seconds spent slow in a row.
    private float _slowTime;

    /// <summary>
    /// Forgets the time spent slow, so a stall needs a full <see cref="StallTuning.MaxTime"/> again.
    /// </summary>
    public void Reset() => _slowTime = 0;

    /// <summary>
    /// Adds this frame's time to the slow time while <paramref name="speed"/> is below the minimum, and clears it otherwise.
    /// </summary>
    /// <returns>True once the body has been slow for long enough to count as stalled.</returns>
    public bool Update(float speed, float gameDt)
    {
        _slowTime = speed < tuning.MinSpeed() ? _slowTime + gameDt : 0;
        return _slowTime >= tuning.MaxTime();
    }
}
```

### StallDetector.cs, version C

Rating: 

```csharp
namespace BodyCheck.Core.Controllers;

/// <summary>
/// Tuning for a <see cref="StallDetector"/>. Each value is a function, read every update, so that it can follow
/// tuning that changes while the game runs.
/// </summary>
public sealed record StallTuning
{
    /// <summary>The speed, in the intended direction, below which a body counts as slow.</summary>
    public required Func<float> MinSpeed { get; init; }

    /// <summary>How long, in game seconds, a body must stay slow to count as stalled.</summary>
    public required Func<float> MaxTime { get; init; }
}

/// <summary>
/// Tells when something that is trying to move has been too slow for too long, such as when it is pushing against
/// something it can't move.
/// </summary>
internal sealed class StallDetector(StallTuning tuning)
{
    private float _slowTime;

    /// <summary>Forgets how long the body has been slow, such as after it changes direction.</summary>
    public void Reset() => _slowTime = 0;

    /// <summary>
    /// Advances the detector by one step.
    /// </summary>
    /// <param name="speed">
    /// The body's speed in the direction it is trying to move: negative when it is moving the other way.
    /// </param>
    /// <param name="gameDt">The step's length in game time.</param>
    /// <returns>Whether the body has been slower than the minimum speed for at least the maximum time.</returns>
    public bool Update(float speed, float gameDt)
    {
        // Any step at speed starts the count again: only an unbroken run of slow steps is a stall.
        _slowTime = speed < tuning.MinSpeed() ? _slowTime + gameDt : 0;
        return _slowTime >= tuning.MaxTime();
    }
}
```

### StallDetector.cs, version D

Rating: 

```csharp
namespace BodyCheck.Core.Controllers;

/// <summary>
/// How a <see cref="StallDetector"/> is tuned. Each value is a function that reads it from the features in effect when
/// it is used, so that a change to them takes effect at once.
/// </summary>
public sealed record StallTuning
{
    /// <summary>The speed, the way the entity is trying to go, below which it counts as slow.</summary>
    public required Func<float> MinSpeed { get; init; }
    /// <summary>How long, in game time, the entity must stay slow to stall.</summary>
    public required Func<float> MaxTime { get; init; }
}

/// <summary>
/// Tells when an entity has stalled: its speed the way it is trying to go has stayed below a minimum for long enough,
/// as when something is in its way.
/// </summary>
/// <param name="tuning">How the detector is tuned.</param>
internal sealed class StallDetector(StallTuning tuning)
{
    // How long the entity has been slow, in game time, without a break.
    private float _slowTime;

    /// <summary>
    /// Starts counting afresh, such as when the entity turns round, so that the slowness of turning isn't a stall.
    /// </summary>
    public void Reset() => _slowTime = 0;

    /// <summary>
    /// Counts <paramref name="gameDt"/> of game time toward a stall if <paramref name="speed"/> is below the minimum,
    /// or starts counting afresh if it isn't.
    /// </summary>
    /// <param name="speed">
    /// The entity's speed the way it is trying to go, which is negative if it is going the other way.
    /// </param>
    /// <returns>Whether the entity has stalled. It stays stalled until it speeds up or is reset.</returns>
    public bool Update(float speed, float gameDt)
    {
        _slowTime = speed < tuning.MinSpeed() ? _slowTime + gameDt : 0;
        return _slowTime >= tuning.MaxTime();
    }
}
```

### StallDetector.cs, version E

Rating: 

```csharp
namespace BodyCheck.Core.Controllers;

/// <summary>
/// How a <see cref="StallDetector"/> is tuned. Each value is a function that reads it from the features in effect when it
/// is used, so that a change to them takes effect at once.
/// </summary>
public sealed record StallTuning
{
    /// <summary>The speed toward the goal below which the entity is slow.</summary>
    public required Func<float> MinSpeed { get; init; }
    /// <summary>How long, in game time, the entity must be slow to stall.</summary>
    public required Func<float> MaxTime { get; init; }
}

/// <summary>
/// Tells when an entity driving toward a goal has stalled: when its speed toward the goal has stayed too low for too
/// long, such as when something is in its way.
/// </summary>
/// <param name="tuning">How the detector is tuned.</param>
internal sealed class StallDetector(StallTuning tuning)
{
    private float _slowTime;

    /// <summary>Starts over, as when the goal changes.</summary>
    public void Reset() => _slowTime = 0;

    /// <summary>
    /// Takes the entity's speed toward its goal, after <paramref name="gameDt"/> more game time, and says whether it has
    /// been slow for long enough to stall.
    /// </summary>
    public bool Update(float speed, float gameDt)
    {
        _slowTime = speed < tuning.MinSpeed() ? _slowTime + gameDt : 0;
        return _slowTime >= tuning.MaxTime();
    }
}
```

### StallDetector.cs, version F

Rating: 

```csharp
namespace BodyCheck.Core.Controllers;

/// <summary>
/// How a <see cref="StallDetector"/> is tuned. Each value is a function that reads it from the features in effect when
/// it is used, so that a change to them takes effect at once.
/// </summary>
public sealed record StallTuning
{
    /// <summary>The speed, in tiles per second, below which the entity is going too slowly.</summary>
    public required Func<float> MinSpeed { get; init; }
    /// <summary>How long, in game time, the speed must stay below <see cref="MinSpeed"/> to stall.</summary>
    public required Func<float> MaxTime { get; init; }
}

/// <summary>
/// Tells when an entity has stalled: when its speed the way it is going has stayed below a minimum for long enough, as
/// when something is in its way. A moment's slowing, such as a bump, isn't a stall.
/// </summary>
/// <param name="tuning">How the detector is tuned.</param>
internal sealed class StallDetector(StallTuning tuning)
{
    // How long, in game time, the speed has stayed below the minimum without a break.
    private float _slowTime;

    /// <summary>Starts counting afresh, such as after turning around, so that the time before doesn't count.</summary>
    public void Reset() => _slowTime = 0;

    /// <summary>
    /// Counts <paramref name="gameDt"/> of game time at <paramref name="speed"/>, and says whether the entity has
    /// stalled. It goes on saying so while the speed stays low, until it is reset.
    /// </summary>
    /// <param name="speed">
    /// The entity's speed the way it is going, negative if it is being pushed back, which counts as slow.
    /// </param>
    public bool Update(float speed, float gameDt)
    {
        _slowTime = speed < tuning.MinSpeed() ? _slowTime + gameDt : 0;
        return _slowTime >= tuning.MaxTime();
    }
}
```

## ScopedTick.cs

### ScopedTick.cs, version A

Rating: 

```csharp
namespace BodyCheck.Core.Tasks;

/// <summary>
/// A tick that waits through a <see cref="TickScheduler"/>, with an optional condition that is checked while waiting.
/// <see cref="Scoped"/> makes a tick whose condition adds another, so work can be stopped by its scope.
/// </summary>
internal sealed class ScopedTick(TickScheduler scheduler, Func<bool>? condition) : ITick
{
    /// <summary>Waits until the next tick.</summary>
    public TickTask NextTick() => Wait(0);

    /// <summary>Waits the given number of game seconds.</summary>
    public TickTask Wait(float seconds) => scheduler.Wait(seconds, condition);

    /// <summary>
    /// Returns a tick like this one, but whose condition also requires <paramref name="inner"/> to hold.
    /// </summary>
    public ITick Scoped(Func<bool> inner) =>
        new ScopedTick(scheduler, condition is null ? inner : () => condition() && inner());

    /// <summary>Waits, one tick at a time, for as long as <paramref name="condition"/> holds.</summary>
    public async TickTask While(Func<bool> condition)
    {
        while (condition())
        {
            await NextTick();
        }
    }

    /// <summary>Runs <paramref name="body"/> each tick while <paramref name="condition"/> holds.</summary>
    public async TickTask While(Func<bool> condition, Action body)
    {
        while (condition())
        {
            body();
            await NextTick();
        }
    }

    /// <summary>
    /// Runs <paramref name="body"/> while <paramref name="condition"/> holds. If the body finishes without time passing,
    /// the loop waits one tick, so it can't spin forever in the same tick.
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
    /// Waits until <paramref name="task"/> completes, then rethrows its exception if it failed.
    /// </summary>
    public async TickTask UntilCompleted(Task task)
    {
        await While(() => !task.IsCompleted);
        task.GetAwaiter().GetResult();
    }

    /// <summary>Waits for each of <paramref name="tasks"/> in turn.</summary>
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

### ScopedTick.cs, version B

Rating: 

```csharp
namespace BodyCheck.Core.Tasks;

/// <summary>
/// Every <see cref="ITick"/>: a scheduler's tick, which makes its waits on the scheduler with its condition, so that
/// the scheduler cancels them once the condition no longer holds. The scheduler's own tick has none, and lasts until
/// the scheduler stops. Everything else it does is built on its waits, so every tick of a scheduler is one scheduler
/// to its tasks, and a chain can await tasks of any of them.
/// </summary>
/// <param name="scheduler">The scheduler the waits are made on.</param>
/// <param name="condition">What the tick's scope lasts while, or null if it has no scope.</param>
internal sealed class ScopedTick(TickScheduler scheduler, Func<bool>? condition) : ITick
{
    public TickTask NextTick() => Wait(0);

    public TickTask Wait(float seconds) => scheduler.Wait(seconds, condition);

    // The outer condition first, so that it can guard the inner one, such as one that reads an entity only while it is
    // there.
    public ITick Scoped(Func<bool> inner) =>
        new ScopedTick(scheduler, condition is null ? inner : () => condition() && inner());

    public async TickTask While(Func<bool> condition)
    {
        while (condition())
        {
            await NextTick();
        }
    }

    public async TickTask While(Func<bool> condition, Action body)
    {
        while (condition())
        {
            body();
            await NextTick();
        }
    }

    public async TickTask While(Func<bool> condition, Func<TickTask> body)
    {
        while (condition())
        {
            double time = Time;
            await body();
            // A body that let no time pass, such as one that finished at once, would otherwise spin within the run.
            if (Time == time)
            {
                await NextTick();
            }
        }
    }

    public async TickTask UntilCompleted(Task task)
    {
        await While(() => !task.IsCompleted);
        // Throws the task's exception if it failed, or an OperationCanceledException if it was cancelled.
        task.GetAwaiter().GetResult();
    }

    public async TickTask All(IEnumerable<TickTask> tasks)
    {
        // Enumerate them all before awaiting any, so that a sequence that starts them as it goes starts them all now.
        List<TickTask> taskList = [.. tasks];
        foreach (var task in taskList)
        {
            await task;
        }
    }

    public float Dt => scheduler.Dt;

    public double Time => scheduler.Time;
}
```

### ScopedTick.cs, version C

Rating: 

```csharp
namespace BodyCheck.Core.Tasks;

/// <summary>
/// Every <see cref="ITick"/>: a scheduler's tick, which makes its waits on its scheduler, with the condition of its
/// scope, if it has one. Everything else it offers is made of those waits, so every tick of a scheduler is the same
/// scheduler to its tasks, and a chain can await tasks of any of them.
/// </summary>
/// <param name="scheduler">The scheduler that makes and finishes its waits.</param>
/// <param name="condition">What its waits last while, or null for the scheduler's own tick, with no scope.</param>
internal sealed class ScopedTick(TickScheduler scheduler, Func<bool>? condition) : ITick
{
    /// <inheritdoc/>
    public TickTask NextTick() => Wait(0);

    /// <inheritdoc/>
    public TickTask Wait(float seconds) => scheduler.Wait(seconds, condition);

    /// <inheritdoc/>
    public ITick Scoped(Func<bool> inner) =>
        // The outer condition first, so that it can guard the inner one, such as one that reads an entity only while it
        // is there.
        new ScopedTick(scheduler, condition is null ? inner : () => condition() && inner());

    /// <inheritdoc/>
    public async TickTask While(Func<bool> condition)
    {
        while (condition())
        {
            await NextTick();
        }
    }

    /// <inheritdoc/>
    public async TickTask While(Func<bool> condition, Action body)
    {
        while (condition())
        {
            body();
            await NextTick();
        }
    }

    /// <inheritdoc/>
    public async TickTask While(Func<bool> condition, Func<TickTask> body)
    {
        while (condition())
        {
            double time = Time;
            await body();
            // A body that let no time pass, such as one that finished at once, would otherwise loop within the run.
            if (Time == time)
            {
                await NextTick();
            }
        }
    }

    /// <inheritdoc/>
    public async TickTask UntilCompleted(Task task)
    {
        // Poll, since awaiting the task would resume off the tick, on whatever thread completed it.
        await While(() => !task.IsCompleted);
        // Throws the task's exception if it failed, or a cancellation if it was cancelled.
        task.GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    public async TickTask All(IEnumerable<TickTask> tasks)
    {
        // Enumerate them all before awaiting any, so that a sequence that starts them as it goes starts every one now.
        List<TickTask> taskList = [.. tasks];
        foreach (var task in taskList)
        {
            await task;
        }
    }

    /// <inheritdoc/>
    public float Dt => scheduler.Dt;

    /// <inheritdoc/>
    public double Time => scheduler.Time;
}
```

### ScopedTick.cs, version D

Rating: 

```csharp
namespace BodyCheck.Core.Tasks;

/// <summary>
/// An <see cref="ITick"/> whose waits all belong to a scope: a condition that must hold for the task to go on.
/// When the condition stops holding, the scheduler ends whatever is waiting rather than resuming it.
/// </summary>
/// <param name="scheduler">The scheduler that resumes waiting tasks.</param>
/// <param name="condition">The scope's condition, or <see langword="null"/> for no scope, which always holds.</param>
internal sealed class ScopedTick(TickScheduler scheduler, Func<bool>? condition) : ITick
{
    /// <inheritdoc/>
    public TickTask NextTick() => Wait(0);

    /// <inheritdoc/>
    public TickTask Wait(float seconds) => scheduler.Wait(seconds, condition);

    /// <inheritdoc/>
    /// <remarks>An inner scope holds only while this one does too.</remarks>
    public ITick Scoped(Func<bool> inner) =>
        new ScopedTick(scheduler, condition is null ? inner : () => condition() && inner());

    /// <inheritdoc/>
    public async TickTask While(Func<bool> condition)
    {
        while (condition())
        {
            await NextTick();
        }
    }

    /// <inheritdoc/>
    public async TickTask While(Func<bool> condition, Action body)
    {
        while (condition())
        {
            body();
            await NextTick();
        }
    }

    /// <inheritdoc/>
    public async TickTask While(Func<bool> condition, Func<TickTask> body)
    {
        while (condition())
        {
            double time = Time;
            await body();
            // A body that finished without waiting would loop forever within one tick, so wait for the next.
            if (Time == time)
            {
                await NextTick();
            }
        }
    }

    /// <inheritdoc/>
    public async TickTask UntilCompleted(Task task)
    {
        await While(() => !task.IsCompleted);
        // Rethrow the task's exception, if it has one, without wrapping it in an AggregateException.
        task.GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    public async TickTask All(IEnumerable<TickTask> tasks)
    {
        // Start every task before awaiting any, so that they run together rather than one after another.
        List<TickTask> taskList = [.. tasks];
        foreach (var task in taskList)
        {
            await task;
        }
    }

    /// <inheritdoc/>
    public float Dt => scheduler.Dt;

    /// <inheritdoc/>
    public double Time => scheduler.Time;
}
```

### ScopedTick.cs, version E

Rating: 

```csharp
namespace BodyCheck.Core.Tasks;

/// <summary>
/// A view of the tick scheduler whose waits are cancelled when a condition stops holding, so a task can't outlive what it is
/// part of.
/// </summary>
/// <param name="scheduler">The scheduler that does the waiting.</param>
/// <param name="condition">What must keep holding for waits to continue, or null if nothing can cancel them.</param>
internal sealed class ScopedTick(TickScheduler scheduler, Func<bool>? condition) : ITick
{
    /// <inheritdoc/>
    public TickTask NextTick() => Wait(0);

    /// <inheritdoc/>
    public TickTask Wait(float seconds) => scheduler.Wait(seconds, condition);

    /// <inheritdoc/>
    // Both conditions must hold, so a scope inside a scope ends when either does.
    public ITick Scoped(Func<bool> inner) =>
        new ScopedTick(scheduler, condition is null ? inner : () => condition() && inner());

    /// <inheritdoc/>
    public async TickTask While(Func<bool> condition)
    {
        while (condition())
        {
            await NextTick();
        }
    }

    /// <inheritdoc/>
    public async TickTask While(Func<bool> condition, Action body)
    {
        while (condition())
        {
            body();
            await NextTick();
        }
    }

    /// <inheritdoc/>
    public async TickTask While(Func<bool> condition, Func<TickTask> body)
    {
        while (condition())
        {
            double time = Time;
            await body();
            // A body that finished without waiting would loop forever within one tick, so make it wait.
            if (Time == time)
            {
                await NextTick();
            }
        }
    }

    /// <inheritdoc/>
    public async TickTask UntilCompleted(Task task)
    {
        await While(() => !task.IsCompleted);
        // The task is done, so this doesn't block. It rethrows the task's exception, if it had one.
        task.GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    public async TickTask All(IEnumerable<TickTask> tasks)
    {
        // Copy first, so that all the tasks are started before waiting on any of them, if the sequence is lazy.
        List<TickTask> taskList = [.. tasks];
        foreach (var task in taskList)
        {
            await task;
        }
    }

    /// <inheritdoc/>
    public float Dt => scheduler.Dt;

    /// <inheritdoc/>
    public double Time => scheduler.Time;
}
```

### ScopedTick.cs, version F

Rating: 

```csharp
namespace BodyCheck.Core.Tasks;

/// <summary>
/// A <see cref="TickScheduler"/>'s tick, as its tasks see it, whose waits are cancelled once its scope ends: once
/// <paramref name="condition"/> no longer holds, or never, if there is none.
/// </summary>
internal sealed class ScopedTick(TickScheduler scheduler, Func<bool>? condition) : ITick
{
    /// <inheritdoc/>
    public TickTask NextTick() => Wait(0);

    /// <inheritdoc/>
    public TickTask Wait(float seconds) => scheduler.Wait(seconds, condition);

    /// <inheritdoc/>
    public ITick Scoped(Func<bool> inner) =>
        // The outer condition first, so that it can guard the inner one, such as that an entity is still there.
        new ScopedTick(scheduler, condition is null ? inner : () => condition() && inner());

    /// <inheritdoc/>
    public async TickTask While(Func<bool> condition)
    {
        while (condition())
        {
            await NextTick();
        }
    }

    /// <inheritdoc/>
    public async TickTask While(Func<bool> condition, Action body)
    {
        while (condition())
        {
            body();
            await NextTick();
        }
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public async TickTask UntilCompleted(Task task)
    {
        await While(() => !task.IsCompleted);
        // Not await: the method would resume off the tick. The task has completed, so this only throws its exception,
        // if it has one, unwrapped, as await would.
        task.GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    public async TickTask All(IEnumerable<TickTask> tasks)
    {
        // Enumerated first, so that every task has started before any is awaited.
        List<TickTask> taskList = [.. tasks];
        foreach (var task in taskList)
        {
            await task;
        }
    }

    /// <inheritdoc/>
    public float Dt => scheduler.Dt;

    /// <inheritdoc/>
    public double Time => scheduler.Time;
}
```

## Muffler.cs

### Muffler.cs, version A

Rating: 

```csharp
using BodyCheck.Core;

using Godot;

namespace BodyCheck.Modes;

/// <summary>
/// Muffles every sound while the game slows down for a phase and while it is paused, with the first low-pass filter on
/// the master bus. Each frame, it sets the filter's cutoff from the <see cref="GameMode"/> and the
/// <see cref="GameModeManager.SlowdownFactor"/>: open while playing and in a cutscene; falling in octaves with the slowdown factor while
/// slowing for a phase, most of the way early on; closed in every mode
/// that pauses. It keeps no state of its own, so it can't drift from the slowdown.
/// <para>
/// The filter is tuned in the bus layout: its cutoff there is the closed one, and its slope how sharply it cuts above
/// that. It is off there, so that sounds aren't
/// muffled in the editor, and this turns it on only while muffling. With no such filter, this does nothing.
/// </para>
/// </summary>
public partial class Muffler : Node
{
    /// <summary>The cutoff, in hertz, with the filter open: the highest the engine allows.</summary>
    private const float OpenCutoff = 20500;

    /// <summary>
    /// The power that shapes the fall while slowing for a phase. Below 1, most of it comes early: at 0.5, it is half
    /// muffled, in octaves, a quarter of the way through the slowdown.
    /// </summary>
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

        // How far muffled, from 0, open, to 1, closed.
        float muffling = _gameModeManager.GameMode switch
        {
            GameMode.Playing or GameMode.Cutscene => 0,
            GameMode.SlowingForPhase => Mathf.Pow((float)Mathf.Clamp(
                (1 - _gameModeManager.SlowdownFactor) / (1 - GameModeState.SlowestSlowdownFactor), 0, 1), SlowdownCurve),
            _ => 1,
        };
        AudioServer.SetBusEffectEnabled(MasterBus, _filterIndex, muffling > 0);
        _filter.CutoffHz = OpenCutoff * Mathf.Pow(_closedCutoff / OpenCutoff, muffling);
    }
}
```

### Muffler.cs, version B

Rating: 

```csharp
using BodyCheck.Core;

using Godot;

namespace BodyCheck.Modes;

/// <summary>
/// Makes each of the game's modes sound as it should, with the low-pass filter on the master bus, which the bus layout
/// leaves off: open while playing and in a cutscene, closing while slowing for a phase, and closed while paused for
/// any reason, at the cutoff the layout gives the filter. See game/bodycheck/modes/architecture.md.
/// </summary>
public partial class Muffler : Node
{
    // The cutoff when open, in Hz: above what anyone hears, so that the open filter muffles nothing.
    private const float OpenCutoff = 20500;

    // The power the slowdown's progress is raised to, below 1 so that most of the muffling comes early on.
    private const float SlowdownCurve = 0.5f;

    // The master bus's index, which is always 0.
    private const int MasterBus = 0;

    private readonly GameModeManager _gameModeManager;

    // The master bus's low-pass filter, and its index among the bus's effects, or null if it has none.
    private AudioEffectLowPassFilter? _filter;
    private int _filterIndex;

    // The cutoff when closed, in Hz: the filter's, as the bus layout gives it.
    private float _closedCutoff;

    /// <param name="gameModeManager">What holds the game's mode, which says how muffled to be.</param>
    public Muffler(GameModeManager gameModeManager)
    {
        _gameModeManager = gameModeManager;
    }

    public override void _Ready()
    {
        // Run while paused, which is when it muffles.
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

        // How muffled to be, from 0, open, to 1, closed. While slowing for a phase, it follows how far the slowdown has
        // gone, from 1 to the slowest factor.
        float muffling = _gameModeManager.GameMode switch
        {
            GameMode.Playing or GameMode.Cutscene => 0,
            GameMode.SlowingForPhase => Mathf.Pow((float)Mathf.Clamp(
                (1 - _gameModeManager.SlowdownFactor) / (1 - GameModeState.SlowestSlowdownFactor), 0, 1), SlowdownCurve),
            _ => 1,
        };

        // Off when open, so that it doesn't touch the sound at all. The cutoff falls from open to closed in octaves, so
        // evenly in pitch.
        AudioServer.SetBusEffectEnabled(MasterBus, _filterIndex, muffling > 0);
        _filter.CutoffHz = OpenCutoff * Mathf.Pow(_closedCutoff / OpenCutoff, muffling);
    }
}
```

### Muffler.cs, version C

Rating: 

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
    // A cutoff above what anyone can hear, so that the filter does nothing audible.
    private const float OpenCutoff = 20500;

    // The exponent on how far the game has slowed: below 1, so that muffling comes in quickly as it starts to slow.
    private const float SlowdownCurve = 0.5f;

    private const int MasterBus = 0;

    private readonly GameModeManager _gameModeManager;
    private AudioEffectLowPassFilter? _filter;
    private int _filterIndex;
    private float _closedCutoff;

    /// <summary>Makes a muffler that follows the game mode that <paramref name="gameModeManager"/> keeps.</summary>
    public Muffler(GameModeManager gameModeManager)
    {
        _gameModeManager = gameModeManager;
    }

    /// <inheritdoc/>
    public override void _Ready()
    {
        // Keep processing while the game is paused, which is when the sound is muffled.
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

    /// <inheritdoc/>
    public override void _Process(double delta)
    {
        if (_filter is null)
        {
            return;
        }

        // From 0 for not muffled to 1 for fully muffled. Slowing for a phase muffles by how far the game has
        // slowed toward its slowest.
        float muffling = _gameModeManager.GameMode switch
        {
            GameMode.Playing or GameMode.Cutscene => 0,
            GameMode.SlowingForPhase => Mathf.Pow((float)Mathf.Clamp(
                (1 - _gameModeManager.SlowdownFactor) / (1 - GameModeState.SlowestSlowdownFactor), 0, 1), SlowdownCurve),
            _ => 1,
        };
        // Turn the filter off when it isn't muffling, so that it costs nothing and colours nothing.
        AudioServer.SetBusEffectEnabled(MasterBus, _filterIndex, muffling > 0);
        // Move the cutoff geometrically between open and closed, since pitch is heard on a logarithmic scale.
        _filter.CutoffHz = OpenCutoff * Mathf.Pow(_closedCutoff / OpenCutoff, muffling);
    }
}
```

### Muffler.cs, version D

Rating: 

```csharp
using BodyCheck.Core;

using Godot;

namespace BodyCheck.Modes;

/// <summary>
/// Muffles the game's sound while time is slowed, by closing the master bus's low-pass filter. The slower the
/// slowdown, the duller the sound, and it is fully open while the game is playing.
/// </summary>
public partial class Muffler : Node
{
    // The filter cutoff, in Hz, when there is no muffling. It is above human hearing, so the sound is unchanged.
    private const float OpenCutoff = 20500;

    // Shapes how quickly the muffling rises as the slowdown deepens. Below 1, it rises quickly at first.
    private const float SlowdownCurve = 0.5f;

    // The index of the master audio bus.
    private const int MasterBus = 0;

    private readonly GameModeManager _gameModeManager;

    // The low-pass filter found on the master bus, or null if there isn't one.
    private AudioEffectLowPassFilter? _filter;

    // The filter's position among the master bus's effects, so it can be switched on and off.
    private int _filterIndex;

    // The cutoff the filter has when the sound is fully muffled, taken from the filter's setting at startup.
    private float _closedCutoff;

    /// <summary>
    /// Creates a muffler that reads the game mode from <paramref name="gameModeManager"/>.
    /// </summary>
    public Muffler(GameModeManager gameModeManager)
    {
        _gameModeManager = gameModeManager;
    }

    /// <summary>
    /// Finds the master bus's low-pass filter, and remembers its settings. Processes even while the game is paused.
    /// </summary>
    public override void _Ready()
    {
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

    /// <summary>
    /// Sets how muffled the sound is for the current game mode, from 0 (open) to 1 (fully muffled).
    /// </summary>
    public override void _Process(double delta)
    {
        if (_filter is null)
        {
            return;
        }

        // No muffling while playing or in a cutscene. While slowing for a phase, muffling follows how deep the slowdown is.
        // Any other mode muffles fully.
        float muffling = _gameModeManager.GameMode switch
        {
            GameMode.Playing or GameMode.Cutscene => 0,
            GameMode.SlowingForPhase => Mathf.Pow((float)Mathf.Clamp(
                (1 - _gameModeManager.SlowdownFactor) / (1 - GameModeState.SlowestSlowdownFactor), 0, 1), SlowdownCurve),
            _ => 1,
        };

        // The filter does nothing when there is no muffling, so it is switched off then.
        AudioServer.SetBusEffectEnabled(MasterBus, _filterIndex, muffling > 0);

        // The cutoff falls geometrically from open to closed as muffling rises, which sounds smoother than a straight line.
        _filter.CutoffHz = OpenCutoff * Mathf.Pow(_closedCutoff / OpenCutoff, muffling);
    }
}
```

### Muffler.cs, version E

Rating: 

```csharp
using BodyCheck.Core;

using Godot;

namespace BodyCheck.Modes;

/// <summary>
/// Muffles all sound by closing the low-pass filter on the master bus whenever the game isn't being played, such as when it is paused
/// or slowing down for a phase.
/// </summary>
public partial class Muffler : Node
{
    // A cutoff above what people can hear, so the filter changes nothing.
    private const float OpenCutoff = 20500;

    // Below 1 muffles quickly at the start of a slowdown, when the change is most noticeable.
    private const float SlowdownCurve = 0.5f;

    // The index of the master bus, which is always first.
    private const int MasterBus = 0;

    private readonly GameModeManager _gameModeManager;
    private AudioEffectLowPassFilter? _filter;
    private int _filterIndex;
    // The cutoff the filter was set to in the editor, which is how muffled sound is when fully muffled.
    private float _closedCutoff;

    /// <summary>Makes a muffler that follows the mode of <paramref name="gameModeManager"/>.</summary>
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

### Muffler.cs, version F

Rating: 

```csharp
using BodyCheck.Core;

using Godot;

namespace BodyCheck.Modes;

/// <summary>
/// Muffles every sound while the game isn't being played, with the low-pass filter on the master bus, which the bus
/// layout leaves off. Each frame, it sets the filter's cutoff from the game's mode: open while playing and in a
/// cutscene, closed while paused for any reason, at the cutoff the layout gives the filter, and, while slowing for a
/// phase, falling from one to the other as the game slows, most of the way early on.
/// </summary>
public partial class Muffler : Node
{
    // The cutoff, in hertz, at which the filter muffles nothing: just above what anyone hears.
    private const float OpenCutoff = 20500;

    // The power the slowdown's progress is raised to, for how muffled sound is while slowing for a phase. Below 1, so
    // that it is muffled most of the way early on, and the slowdown is heard at once.
    private const float SlowdownCurve = 0.5f;

    // The master bus's index, which every sound ends up on.
    private const int MasterBus = 0;

    private readonly GameModeManager _gameModeManager;

    // The master bus's low-pass filter, or null if it has none, and its index among the bus's effects.
    private AudioEffectLowPassFilter? _filter;
    private int _filterIndex;

    // The cutoff, in hertz, at which the filter muffles sound most: the one the bus layout gives it.
    private float _closedCutoff;

    /// <param name="gameModeManager">What holds the game's mode, and the slowdown for a phase.</param>
    public Muffler(GameModeManager gameModeManager)
    {
        _gameModeManager = gameModeManager;
    }

    public override void _Ready()
    {
        // It must muffle sound while the game is paused.
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

        // How muffled sound is, from 0, not at all, to 1, most. While slowing for a phase, it follows how far the
        // slowdown has gone, from no slowdown to the slowest.
        float muffling = _gameModeManager.GameMode switch
        {
            GameMode.Playing or GameMode.Cutscene => 0,
            GameMode.SlowingForPhase => Mathf.Pow((float)Mathf.Clamp(
                (1 - _gameModeManager.SlowdownFactor) / (1 - GameModeState.SlowestSlowdownFactor), 0, 1), SlowdownCurve),
            _ => 1,
        };
        // Off while it muffles nothing, so that it costs nothing and colours nothing.
        AudioServer.SetBusEffectEnabled(MasterBus, _filterIndex, muffling > 0);
        // The cutoff falls in octaves, from open to closed, as hearing judges pitch.
        _filter.CutoffHz = OpenCutoff * Mathf.Pow(_closedCutoff / OpenCutoff, muffling);
    }
}
```
