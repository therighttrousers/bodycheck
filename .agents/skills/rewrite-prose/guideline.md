# Writing comments and docs

## Doc comments are contracts

A type or member is an abstraction boundary. Write its doc comment for someone who will use it without reading its implementation, and tell them what they need to use it correctly:

- **Requirements:** what the caller must do or supply. State them as instructions: "Supply functions that read the current features", "Call this when the body changes direction", "Should be positive".
- **Provisions:** what the caller gets in return: the result, and any lasting effect on the rest of the system.
- **When to use it**, for a reusable type: "Use this to cancel tasks when their dependencies are no longer valid".

Leave out what the signature already says, and leave out how the implementation works. A doc comment that narrates everything that happens inside a member is a sign that the member, or the comment, is at the wrong level. If callers need many details, consider changing the design.

Name a concept once and use that name consistently, such as "the stall timer". If the name exposes an internal concept, make sure callers need it.

## Inline comments explain what the code can't

Most code needs no comment. Write one for:

- surprising behaviour, hidden dependencies and invariants;
- ordering that matters, and aliasing;
- fragility, such as relying on an engine limit;
- the minimum context needed to understand a dense passage.

Explain why, or what must stay true. Don't restate the logic.

## Plain, standard English

- Use the standard technical term, the one a reader would search for: "shortest path", not "best way"; "request", not "ask".
- Prefer a precise verb to a general one such as "hold", "keep", "go" or "take".
- Start a summary with what it describes ("The thresholds for a stall detector"), not with "What ...".
- Pronouns refer back to something already named, never forward.
- One idea per sentence.

## Docs

A doc, such as an `architecture.md` or a design doc, is written for someone who is about to work on the code it describes, and has read the docs above it but not this one.

- Say what is true now, in the present tense.
- Write a decision-record citation in backticks, such as `043 §1.7`, beside the fact it supports. Cite a record once per paragraph or bullet that it supports, not on every sentence.
- Give a reason in the sentence that states the rule it explains, with "so that" or "because", rather than in a sentence of its own.
- Lead with what a reader must know to work here: what the part is for, the rules that code in it must follow, and why, where the reason isn't obvious.
- One topic per paragraph or bullet, in short sentences. Split a sentence that strings facts together with colons, semicolons and commas.
- Use the code's names for things, in backticks, so a reader can search for them.

## Examples

These were written by the project's lead for other files in this codebase.

```csharp
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

    /// <summary>Resets the stall timer. Call this if the body changes direction or stops trying to move.</summary>
    public void Reset() => _slowTime = 0;

    /// <summary>
    /// Adds <paramref name="gameDt"/> to the stall timer if <paramref name="speed"/> is below the minimum, and clears
    /// it otherwise.
    /// </summary>
    /// <param name="speed">
    /// The body's signed speed in the intended direction. It should be negative when velocity is opposite. Consider
    /// passing a scalar projection.
    /// </param>
    /// <param name="gameDt">The physics step's length in game time.</param>
    /// <returns>Whether the body has been slower than the minimum speed for at least the maximum time.</returns>
    public bool Update(float speed, float gameDt)
```

```csharp
/// <summary>
/// A view of the tick scheduler whose waits are cancelled when a condition stops holding. Use this to scope async
/// tasks based on validity; e.g. to cancel after a timeout or keep running only while dependencies are alive/valid.
/// </summary>
internal sealed class ScopedTick(TickScheduler scheduler, Func<bool>? condition) : ITick
{
    /// <summary>
    /// Returns a tick like this one, but whose condition also requires <paramref name="inner"/> to hold; i.e.
    /// it returns a tick with a nested scope.
    /// </summary>
    public ITick Scoped(Func<bool> inner) =>

    /// <summary>
    /// Runs <paramref name="body"/> while <paramref name="condition"/> holds (and the tick stays in scope). If the body
    /// finishes without time passing, the loop waits one tick, so it can't spin forever in the same tick.
    /// </summary>
    public async TickTask While(Func<bool> condition, Func<TickTask> body)
```

```csharp
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
    // The cutoff, in hertz, at which the filter muffles nothing: just above human hearing, and the highest the
    // engine allows.
    private const float OpenCutoff = 20500;

        // Disabled when open, so the filter costs nothing during play. The cutoff is interpolated by ratio, since
        // pitch is logarithmic.
        AudioServer.SetBusEffectEnabled(MasterBus, _filterIndex, muffling > 0);
```
