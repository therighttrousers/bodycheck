using BodyCheck.Core.Entities;

using Godot;

namespace BodyCheck.Core.Controllers;

/// <summary>
/// How a <see cref="SpawnAction"/> is tuned. Each value is a function that reads it from the features in effect when it
/// is used, so that a change to them takes effect at once. See the player's, in <see cref="Features"/>, for what each
/// means.
/// </summary>
public sealed record SpawnTuning
{
    public required Func<float> CooldownTime { get; init; }
    public required Func<float> CancelCooldownTime { get; init; }
    public required Func<SpawnMotion> Motion { get; init; }

    public static SpawnTuning Player { get; } = new()
    {
        CooldownTime = () => Features.Current.PlayerSpawnCooldownTime,
        CancelCooldownTime = () => Features.Current.PlayerSpawnCancelCooldownTime,
        Motion = () => Features.Current.PlayerSpawnMotion,
    };
}

/// <summary>Spawning a block under an entity, to stand on: the player's block.</summary>
/// <param name="entity">The entity that spawns the block.</param>
/// <param name="block">
/// The block, which the entity spawns. Until then it is disabled and hidden, so that it is out of the world.
/// </param>
/// <param name="tuning">How the action is tuned.</param>
public sealed class SpawnAction(Entity entity, Entity block, SpawnTuning tuning)
{
    // In game time: how long until the block can be spawned again.
    private float _cooldownLeft;

    /// <summary>
    /// Spawns the block under the entity, if it spawns on this tick, after the cooldown. The block goes at the
    /// entity's height, as near its center as it fits beside the tiles, and as far straight down from there, through
    /// entities, as it can go until it is under the entity, or meets a tile. Then the entity moves up through entities by
    /// as much as the block fell short, onto it: on the ground, by the block's height, and in the open air, not at all.
    /// Where the block doesn't fit, or the entity has no room to rise onto it, the spawn is cancelled. There is only one block, so spawning it
    /// takes it from wherever it was. The moves wait until after the entity steps, like a phase's.
    /// </summary>
    /// <param name="spawns">Whether the entity spawns its block on this tick.</param>
    /// <param name="gameDt">The tick's length in game time.</param>
    /// <param name="defer">An object that allows running actions later in the tick.</param>
    public void Apply(bool spawns, float gameDt, IDefer defer)
    {
        Step(spawns, defer);
        _cooldownLeft = Math.Max(0, _cooldownLeft - gameDt);
    }

    private void Step(bool spawns, IDefer defer)
    {
        if (!spawns || _cooldownLeft > 0)
        {
            return;
        }

        if (SpawnPlace() is not (Vector3 place, float rise))
        {
            _cooldownLeft = tuning.CancelCooldownTime();
            entity.RaiseCue(EntityCue.SpawnCancelled);
            return;
        }
        _cooldownLeft = tuning.CooldownTime();

        Vector3 velocity = tuning.Motion() == SpawnMotion.WithPlayer ? entity.LinearVelocity : Vector3.Zero;
        Vector3 center = entity.Position;
        defer.RunAfterEntitySteps(() =>
        {
            // A new block, as far as anyone can tell: back in the world, level, and moving only as the spawn says.
            block.ProcessMode = Node.ProcessModeEnum.Inherit;
            block.Visible = true;
            block.GlobalTransform = Transform3D.Identity;
            block.LinearVelocity = velocity;
            block.AngularVelocity = Vector3.Zero;
            block.MoveTo(place);
            if (rise > 0)
            {
                entity.MoveTo(center + new Vector3(0, rise, 0));
            }
            block.RaiseCue(EntityCue.Spawned);
        });
    }

    /// <summary>
    /// Where the block goes, and how far the entity rises onto it, or null if it doesn't fit beside the tiles or the
    /// entity has no room to rise onto it. Found by casting boxes against tiles, as of the engine's last step, each
    /// from where it is clear of them, so that no move starts inside a tile.
    /// </summary>
    private (Vector3 Place, float Rise)? SpawnPlace()
    {
        Vector2 entityHalf = entity.HalfSize;
        Vector2 blockHalf = block.HalfSize;
        Vector3 center = entity.Position;

        // A box of the entity's width and the block's height, at the entity's center, is inside the entity, so clear of
        // tiles. How far it can move to each side is the room the block has there. The block is wider than the entity
        // by `extra`, so it fits where the room on both sides adds up to that, and starts as near the entity's center as
        // it fits. The casts may come up short by the contact tolerance, so a block that only just fits, as in a shaft
        // one tile wide, counts as fitting, and starts in the middle of the room.
        float extra = 2 * (blockHalf.X - entityHalf.X);
        var side = new Vector2(entityHalf.X, blockHalf.Y);
        float left = entity.RoomToMove(side, center, Vector3.Left, extra, PhysicsLayer.Static);
        float right = entity.RoomToMove(side, center, Vector3.Right, extra, PhysicsLayer.Static);
        if (left + right < extra - Entity.ContactTolerance)
        {
            return null;
        }
        // Not Math.Clamp, which throws if rounding puts the bounds the wrong way round when the block only just fits.
        float shift = left + right < extra
            ? (right - left) / 2
            : MathF.Min(MathF.Max(0, extra / 2 - left), right - extra / 2);
        Vector3 start = center + new Vector3(shift, 0, 0);

        // From there, the block falls until it is under the entity, or meets a tile. The entity rises by as much as it
        // falls short, which needs that much room above the entity.
        float drop = entityHalf.Y + blockHalf.Y;
        float fall = entity.RoomToMove(blockHalf, start, Vector3.Down, drop, PhysicsLayer.Static);
        float rise = drop - fall;
        if (rise > 0 && entity.RoomToMove(entityHalf, center, Vector3.Up, rise, PhysicsLayer.Static) < rise)
        {
            return null;
        }
        return (start - new Vector3(0, fall, 0), rise);
    }
}
