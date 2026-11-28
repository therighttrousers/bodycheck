# Pairwise choices

Each pair shows two versions of the same comment, in a random order, above the code it describes. After "Choice:", write 1 or 2 for the clearer one, or = if neither is clearer. Add a note if you like. The key is in `pairs-key.md`.

## Pair 1

`src/BodyCheck.Core/Controllers/PatrolBehaviour.cs`, before line 12 of version 1

Version 1:

```csharp
    /// <summary>The speed, the way the walker is walking, below which it counts as slow.</summary>
```
Version 2:

```csharp
    /// <summary>The speed the way the walker moves below which it is slow.</summary>
```
Code:

```csharp
    public required Func<float> StallSpeed { get; init; }
    public required Func<float> StallTime { get; init; }
    public required Func<float> StallHeat { get; init; }
    public required Func<float> StallMaxHeat { get; init; }
```

Choice: 1

## Pair 2

`src/BodyCheck.Core/Controllers/PatrolBehaviour.cs`, before line 20 of version 1

Version 1:

```csharp
    /// <summary>How quickly stall heat is shed, in heat per game-time second.</summary>
```
Version 2:

```csharp
    /// <summary>How quickly heat is shed, in heat per game-time second.</summary>
```
Code:

```csharp
    public required Func<float> StallCooldownRate { get; init; }

    public static PatrolTuning Robot { get; } = new()
    {
```

Choice: =

## Pair 3

`src/BodyCheck.Core/Controllers/PlatformController.cs`, before line 60 of version 1

Version 1:

```csharp
    /// <inheritdoc/>
```
Version 2:

```csharp
    /// <summary>The platform this controller flies.</summary>
```
Code:

```csharp
    public PlatformEntity Entity => _entity;

    public void Apply(float gameDt, float realDt, IDefer defer)
    {
```

Choice: 2

Note: I compared against the inherited docstring, which only mentioned an `Entity`.

## Pair 4

`src/BodyCheck.Core/Controllers/PlatformController.cs`, before line 149 of version 1

Version 1:

```csharp
        // There is no previous step to compare with on the first step.
```
Version 2:

```csharp
        // No previous step to compare with.
```
Code:

```csharp
        if (_lastDt == 0)
        {
            return 0;
        }
```

Choice: 1

## Pair 5

`game/bodycheck/state/GameState.cs`, before line 41 of version 1

Version 1:

```csharp
    // Each world's current respawn's id, by the world's name.
```
Version 2:

```csharp
    // The respawn point for each world, by world name.
```
Code:

```csharp
    private readonly Dictionary<string, string> _respawnIds = [];

    private readonly Dictionary<string, EntityData> _saved = [];

```

Choice: 2

## Pair 6

`game/bodycheck/state/GameState.cs`, before line 79 of version 1

Version 1:

```csharp
    /// <summary>
    /// Returns the data to save this game, including the saved data of entities that no world has filled in since it
    /// was loaded.
    /// </summary>
    /// <param name="saved">When the game is saved.</param>
```
Version 2:

```csharp
    /// <summary>
    /// Creates a save of this game, saved at <paramref name="saved"/>.
    /// </summary>
    /// <remarks>
    /// The save contains the saved data of every entity, including entities in worlds that haven't been filled since
    /// the game was loaded. It shares no collections with this game state, but it shares <see cref="Story"/>.
    /// </remarks>
```
Code:

```csharp
    public SaveData ToSave(DateTimeOffset saved)
    {
        var entities = new Dictionary<string, EntityData>(_saved);
        foreach (EntityState entity in _entities.Values)
```

Choice: 1

Note: Version 2 could give the wrong impression that `ToSave` creates a save *file* rather than just save *data*.

## Pair 7

`src/BodyCheck.Core/Controllers/PatrolBehaviour.cs`, before line 74 of version 1

Version 1:

```csharp
        // Cool down whatever happens, so that a walker that isn't patrolling still recovers.
```
Version 2:

```csharp
        // Cools down even when not patrolling, so a walker that overheated recovers while it is told to stand still.
```
Code:

```csharp
        _stallHeat.CoolDown(gameDt);

        if (!patrols)
        {
```

Choice: 2

## Pair 8

`src/BodyCheck.Core/Controllers/PatrolBehaviour.cs`, before line 23 of version 1

Version 1:

```csharp
    /// <summary>The heat at which the walker stops patrolling until it cools down.</summary>
```
Version 2:

```csharp
    /// <summary>The heat at which a walker gives up patrolling until it cools down.</summary>
```
Code:

```csharp
    public required Func<float> StallMaxHeat { get; init; }

    public required Func<float> StallCooldownRate { get; init; }

```

Choice: 2

## Pair 9

`game/bodycheck/state/GameState.cs`, before line 163 of version 1

Version 1:

```csharp
    /// <summary>The translation context of entity kinds' names.</summary>
```
Version 2:

```csharp
    /// <summary>The context of a kind's name, as a speaker, in translation.</summary>
```
Code:

```csharp
    internal const string KindContext = "kind";

    internal const string NameContext = "entity name";

```

Choice: 1

## Pair 10

`src/BodyCheck.Core/Controllers/PatrolBehaviour.cs`, before line 12 of version 1

Version 1:

```csharp
    /// <summary>The speed, in the direction of patrol, below which a walker counts as slow.</summary>
```
Version 2:

```csharp
    /// <summary>The speed below which a patroller counts as too slow.</summary>
```
Code:

```csharp
    public required Func<float> StallSpeed { get; init; }

    public required Func<float> StallTime { get; init; }

```

Choice: =

Note: Each leaves out something a little bit helpful.

## Pair 11

`src/BodyCheck.Core/Controllers/PatrolBehaviour.cs`, before line 69 of version 1

Version 1:

```csharp
    /// <summary>Returns how the walker should move during this physics step. Call this once per physics step.</summary>
    /// <param name="patrols">
    /// Whether the walker should patrol. If it shouldn't, the intent is to stand still facing <paramref name="facing"/>.
    /// </param>
    /// <param name="facing">
    /// The walker's current facing: -1 for left, 1 for right, or 0 for neither. A patrol that starts with no contact to
    /// turn away from starts in this direction.
    /// </param>
    /// <param name="gameDt">The physics step's length in game time.</param>
```
Version 2:

```csharp
    /// <summary>
    /// Patrols, or, if it doesn't on this tick, stands still. Off tiles, such as in the air or on another entity, it
    /// stands still and faces the camera, looking both ways. When stalled, it turns round, and when it has stalled too
    /// often, it rests facing the camera until it has cooled down.
    /// </summary>
    /// <param name="patrols">Whether it patrols on this tick, rather than standing still for something else.</param>
    /// <param name="facing">The way the walker faces now, which it goes on in.</param>
    /// <param name="gameDt">The tick's length in game time.</param>
```
Code:

```csharp
    public PatrolIntent Apply(bool patrols, float facing, float gameDt)
    {
        _stallHeat.CoolDown(gameDt);

```

Choice: 1

Note: This contest isn't even close. I find version 1 extremely clear.

## Pair 12

`game/bodycheck/state/GameState.cs`, before line 24 of version 1

Version 1:

```csharp
    // By ID.
```
Version 2:

```csharp
    // Entity states by ID, including the player's.
```
Code:

```csharp
    private readonly Dictionary<string, EntityState> _entities = [];

    private readonly Dictionary<string, EntityState> _entitiesByName = [];

```

Choice: 2

Note: Version 1 looks pretty bad until you read the code; then it says just enough. I chose version 2 only because version 1 could confuse readers purely by how short it is.

## Pair 13

`game/bodycheck/state/GameState.cs`, before line 156 of version 1

Version 1:

```csharp
    /// <summary>The story state: how far the game's story has got.</summary>
```
Version 2:

```csharp
    /// <summary>The progress through the story.</summary>
```
Code:

```csharp
    public StoryState Story { get; }

    public string? GetRespawnId(string worldName) => _respawnIds.GetValueOrDefault(worldName);

```

Choice: 2

Note: I don't really like either of these. `Story` will also contain - and currently only contains - auxiliary state variables that have little to do with progress.

## Pair 14

`game/bodycheck/state/GameState.cs`, before line 207 of version 1

Version 1:

```csharp
    /// <summary>
    /// The character whose name, translated, is <paramref name="name"/>, as a dialogue names its speaker: the Voice, or
    /// any entity filled in so far; or null if there is none.
    /// </summary>
```
Version 2:

```csharp
    /// <summary>
    /// The character a dialogue's line names as its speaker, by its translated name, among the Voice and every entity
    /// filled in so far, or null if there is none.
    /// </summary>
```
Code:

```csharp
    public CharacterState? GetSpeaker(string name) =>
        Voice.Name == name ? Voice : _entities.Values.FirstOrDefault(entity => entity.Name == name);

    IGameState.IPlayerState IGameState.Player => Player;
```

Choice: 2

Note: Tough choice. Both are made awkward by trying to communicate that a non-entity character state might be returned, and overindexing on it possibly representing the Voice. It would be clearer to leave out non-entity character states entirely.

## Pair 15

`src/BodyCheck.Core/Controllers/PatrolBehaviour.cs`, before line 51 of version 1

Version 1:

```csharp
    // The way the walker is moving: -1, 1, or 0 when it has stopped, and chooses a way again when it next can.
```
Version 2:

```csharp
    // The way the walker is walking: -1, 1, or 0 when it isn't.
```
Code:

```csharp
    private float _move;
    private readonly StallDetector _stall = new(new StallTuning { MinSpeed = tuning.StallSpeed, MaxTime = tuning.StallTime });
    private readonly OverheatDetector _stallHeat = new(new OverheatTuning
    {
```

Choice: 2

## Pair 16

`game/bodycheck/state/GameState.cs`, before line 118 of version 1

Version 1:

```csharp
    /// <summary>
    /// Fills in the states that <paramref name="world"/>'s entities lack, as the loaded save, if any, says they are, or
    /// else as its data says they start, and keeps those it has, before the world is built. A saved record of the wrong
    /// kind is ignored. Each entity's name is taken from its data again, since a level may have been edited since. No
    /// two entities in the data have the same name, so a name that another entity's state still has was that entity's
    /// before an edit, and goes to this one.
    /// </summary>
    /// <exception cref="ArgumentException">One of its entities is named as the player is.</exception>
```
Version 2:

```csharp
    /// <summary>
    /// Adds the state of each entity in <paramref name="world"/> that this doesn't have yet, from its saved data if
    /// there is any, and updates every entity's given name. Call this when a world loads, before anything reads the
    /// state of its entities. It is safe to call again with the same world.
    /// </summary>
    /// <exception cref="ArgumentException">An entity in the world is given the player's name.</exception>
```
Code:

```csharp
    public void Fill(World world)
    {
        foreach (EntityConfig config in world.Bodies.Concat(world.Controls))
        {
```

Choice: 2

Note: I find version 2 to be very clear.

## Pair 17

`game/bodycheck/state/GameState.cs`, before line 193 of version 1

Version 1:

```csharp
    /// <summary>A speaker in dialogue that isn't an entity: a voice the player hears.</summary>
```
Version 2:

```csharp
    /// <summary>The speaker that isn't an entity, for lines spoken by no one in the world.</summary>
```
Code:

```csharp
    public CharacterState Voice { get; } = new(Translatable.Mark("Voice", KindContext));

    public EntityState GetEntity(string id) => _entities[id];

```

Choice: 1

Note: Version 1 is fairly clear. Version 2 gives the wrong impression of the Voice. It's a character, not *no* character.

## Pair 18

`src/BodyCheck.Core/Controllers/PlatformController.cs`, before line 106 of version 1

Version 1:

```csharp
        // Hold the platform on the line against whatever pushes it off, with the same limit on thrust.
```
Version 2:

```csharp
        // Holds the platform on the line through the range, against loads that push it off.
```
Code:

```csharp
        float hold = Math.Clamp(
            Spring.CriticallyDampedForce(
                position.Dot(_across),
                velocity.Dot(_across),
```

Choice: 1

## Pair 19

`src/BodyCheck.Core/Controllers/PlatformController.cs`, before line 96 of version 1

Version 1:

```csharp
        // Pulls the platform back onto the line through the ends, across the direction of travel.
```
Version 2:

```csharp
        // The hold force pulls the platform back onto the line through the range's ends.
```
Code:

```csharp
        float hold = Math.Clamp(
            Spring.CriticallyDampedForce(
                position.Dot(_across),
                velocity.Dot(_across),
```

Choice: =

Note: Both use unexpected terminology given the terminology in the code they describe - "pull" instead of "thrust." Perhaps they're overindexing on the word "spring." It's confusing to mention the range's endpoints, which were only used to compute `_across` (orthogonal to `_along`, was computed from the ends) - and to call them "ends."

## Pair 20

`src/BodyCheck.Core/Controllers/PlatformController.cs`, before line 42 of version 1

Version 1:

```csharp
    /// <summary>
    /// Creates a controller that flies <paramref name="entity"/> between <paramref name="rangeA"/> and
    /// <paramref name="rangeB"/>, toward B first. Supply two distinct points.
    /// </summary>
```
Version 2:

```csharp
    /// <param name="entity">The platform, which the controller controls.</param>
    /// <param name="rangeA">One end of the line that the platform's center moves along.</param>
    /// <param name="rangeB">
    /// The other end, which the platform heads for first. Must not be the same as <paramref name="rangeA"/>.
    /// </param>
```
Code:

```csharp
    public PlatformController(PlatformEntity entity, Vector2 rangeA, Vector2 rangeB)
    {
        _entity = entity;
        _rangeA = rangeA;
```

Choice: 1

## Pair 21

`src/BodyCheck.Core/Controllers/PatrolBehaviour.cs`, before line 38 of version 1

Version 1:

```csharp
/// <summary>How a patrolling walker intends to move.</summary>
/// <param name="Move">The direction to walk in: -1 for left, 1 for right, or 0 to stand still.</param>
/// <param name="Facing">The direction to face: -1 for left, 1 for right, or 0 for neither.</param>
```
Version 2:

```csharp
/// <summary>
/// What a patrolling walker wants to do this step. <c>Move</c> is the direction to walk: 1 for right, -1 for left,
/// and 0 to stand still. <c>Facing</c> is the direction the walker faces, in the same units.
/// </summary>
```
Code:

```csharp
public readonly record struct PatrolIntent(float Move, float Facing);

public sealed class PatrolBehaviour(WalkerEntity entity, PatrolTuning tuning)
{
```

Choice: 1

Note: Version 2's use of the word "units" is confusing.

## Pair 22

`src/BodyCheck.Core/Controllers/PatrolBehaviour.cs`, before line 18 of version 1

Version 1:

```csharp
    /// <summary>The heat added each time the walker stalls.</summary>
```
Version 2:

```csharp
    /// <summary>The heat each stall adds.</summary>
```
Code:

```csharp
    public required Func<float> StallHeat { get; init; }

    public required Func<float> StallMaxHeat { get; init; }

```

Choice: 1

## Pair 23

`src/BodyCheck.Core/Controllers/PatrolBehaviour.cs`, before line 53 of version 1

Version 1:

```csharp
/// <summary>
/// Walks a walker back and forth along the ground, turning when it stalls against something.
/// </summary>
/// <remarks>
/// Stalls build up heat, which cools over time. A walker that stalls too often, such as one boxed in or pushed
/// against, overheats and stands still until it cools down.
/// </remarks>
```
Version 2:

```csharp
/// <summary>
/// Walks a walker back and forth along the ground, turning back when it is blocked. A walker blocked on both sides
/// stands still, and one that is blocked too often gives up until it cools down, so a walker that is trapped doesn't
/// turn back and forth forever. Use this in a walker's controller, which applies the intent it returns.
/// </summary>
/// <param name="entity">The walker that patrols.</param>
/// <param name="tuning">The thresholds for stalling and for giving up.</param>
```
Code:

```csharp
public sealed class PatrolBehaviour(WalkerEntity entity, PatrolTuning tuning)
{
    private const float ArbitraryFirstMove = 1;

```

Choice: 1

Note: Version 1 is wonderfully clear.

## Pair 24

`src/BodyCheck.Core/Controllers/PatrolBehaviour.cs`, before line 71 of version 1

Version 1:

```csharp
    /// <summary>
    /// Decides the way to move and the way to face for a tick of <paramref name="gameDt"/> game time. The walker
    /// patrols only while it stands on tiles: on anything else, such as a block, a platform or another walker, or in
    /// the air, it stands still, facing the camera.
    /// </summary>
    /// <param name="patrols">
    /// Whether to patrol on this tick. If not, it stands still, facing as it does, and starts over when it next
    /// patrols.
    /// </param>
    /// <param name="facing">The way the walker faces now: -1, 1, or 0 toward the camera.</param>
    /// <param name="gameDt">The game time since the last tick.</param>
```
Version 2:

```csharp
    /// <summary>Decides which way the walker walks and faces on this tick.</summary>
    /// <param name="patrols">
    /// Whether the controller lets it patrol now. If not, it stands still, and faces as it did, so that its controller
    /// can face it however it needs, such as toward a target.
    /// </param>
    /// <param name="facing">The way the walker faces now: -1 or 1, or 0, toward the camera.</param>
    /// <param name="gameDt">The tick's length, in game time.</param>
```
Code:

```csharp
    public PatrolIntent Apply(bool patrols, float facing, float gameDt)
    {
        _stallHeat.CoolDown(gameDt);

```

Choice: 2

## Pair 25

`game/bodycheck/state/GameState.cs`, before line 77 of version 1

Version 1:

```csharp
    /// <summary>When the game was started, which a menu of saves would show.</summary>
```
Version 2:

```csharp
    /// <summary>When the game was started.</summary>
```
Code:

```csharp
    public DateTimeOffset Started { get; }

    public SaveData ToSave(DateTimeOffset saved)
    {
```

Choice: 1

Note: Either is fine. Version 1 edges out version 2 because it gives a reason for the data.

## Pair 26

`src/BodyCheck.Core/Controllers/PlatformController.cs`, before line 81 of version 1

Version 1:

```csharp
        // A stall only counts between the ends. Near an end the platform turns anyway.
```
Version 2:

```csharp
        // A stall counts only between the turning zones. Just after turning, the platform is slow in its new heading
        // until it reverses, and that isn't being blocked.
```
Code:

```csharp
        bool between = fraction > turnFraction && fraction < 1 - turnFraction;
        bool stalled = _stall.Update(velocity.Dot(_towardB ? _along : -_along), gameDt);

        if (atEnd || (stalled && between))
```

Choice: 1

## Pair 27

`game/bodycheck/state/GameState.cs`, before line 206 of version 1

Version 1:

```csharp
    /// <summary>The state of the entity with the untranslated name <paramref name="name"/>.</summary>
    /// <exception cref="KeyNotFoundException">No entity filled in so far has that name.</exception>
```
Version 2:

```csharp
    /// <summary>Returns the state of the entity with the given name <paramref name="name"/>, untranslated.</summary>
    /// <exception cref="KeyNotFoundException">No entity in a filled world has the name.</exception>
```
Code:

```csharp
    public EntityState GetEntityNamed(string name) => _entitiesByName[name];

    public CharacterState? GetSpeaker(string name) =>
        Voice.Name == name ? Voice : _entities.Values.FirstOrDefault(entity => entity.Name == name);
```

Choice: 1

## Pair 28

`game/bodycheck/state/GameState.cs`, before line 179 of version 1

Version 1:

```csharp
    /// <summary>The translation context for the names given to entities.</summary>
```
Version 2:

```csharp
    /// <summary>The translation context of entities' given names.</summary>
```
Code:

```csharp
    internal const string NameContext = "entity name";

    private static T CastEntity<T>(object entity, string id) => entity switch
    {
```

Choice: =

## Pair 29

`src/BodyCheck.Core/Controllers/PatrolBehaviour.cs`, before line 79 of version 1

Version 1:

```csharp
        // Cool down first, even when not patrolling, so the heat still drains while the walker is idle.
```
Version 2:

```csharp
        // Cools down even while not patrolling, so giving up wears off.
```
Code:

```csharp
        _stallHeat.CoolDown(gameDt);

        if (!patrols)
        {
```

Choice: 1

## Pair 30

`game/bodycheck/state/GameState.cs`, before line 51 of version 1

Version 1:

```csharp
    /// <summary>
    /// Creates the state of a saved game, to continue it.
    /// </summary>
    /// <remarks>
    /// Only the player's state is created now. Each other entity's state is created from its saved data when a world
    /// that contains it is passed to <see cref="Fill"/>.
    /// </remarks>
```
Version 2:

```csharp
    /// <summary>
    /// Restores the state of a saved game. Other entities' states are restored as worlds fill them in; see
    /// <see cref="Fill"/>.
    /// </summary>
```
Code:

```csharp
    public GameState(SaveData save)
        : this(save.Started, save.Story, save.Entities.GetValueOrDefault(Constants.PlayerId) as PlayerData)
    {
        WorldName = save.WorldName;
```

Choice: 2

Note: To be clear, version 2 would be much improved if "other" contrasted with "player."

## Pair 31

`src/BodyCheck.Core/Controllers/PatrolBehaviour.cs`, before line 18 of version 1

Version 1:

```csharp
    /// <summary>The heat at which a patroller overheats and stops patrolling until it cools down.</summary>
```
Version 2:

```csharp
    /// <summary>The heat at which a walker that keeps stalling gives up and stands still until it cools.</summary>
```
Code:

```csharp
    public required Func<float> StallMaxHeat { get; init; }
    public required Func<float> StallCooldownRate { get; init; }

    public static PatrolTuning Robot { get; } = new()
```

Choice: 2

## Pair 32

`src/BodyCheck.Core/Controllers/PlatformController.cs`, before line 121 of version 1

Version 1:

```csharp
        // Its strain is how much of its most it thrusts against what pushes it around: across its line, or against its
        // drive. Smoothed, so that the animation tree, which looks at it only when animations step, sees a short rise.
```
Version 2:

```csharp
        // How hard it works against what pushes it around, as a fraction of its most: the hold, or how much of its
        // drive a load fought, whichever is more. Moving itself along its line doesn't count. It is smoothed, since the
        // animation tree reads it only when animations step, and would miss a short rise.
```
Code:

```csharp
        float strain = MathF.Min(1, MathF.Max(MathF.Abs(hold), DriveFought(velocity)) / maxThrust);
        _entity.Strain = Smoothing.Exponential(_entity.Strain, strain, features.PlatformStrainTime, gameDt);
        _lastDrive = drive;
        _lastHeading = heading;
```

Choice: =

Note: These are both awkward and difficult to understand in their own ways.

## Pair 33

`game/bodycheck/state/GameState.cs`, before line 151 of version 1

Version 1:

```csharp
    /// <summary>What has happened in the story, which the dialogue reads and sets.</summary>
```
Version 2:

```csharp
    /// <summary>The story's state, with a state for each beat, which the dialogue reads and sets.</summary>
```
Code:

```csharp
    public StoryState Story { get; }

    public string? GetRespawnId(string worldName) => _respawnIds.GetValueOrDefault(worldName);

```

Choice: 2
