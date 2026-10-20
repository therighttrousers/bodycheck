using BodyCheck.Core.Entities;

using Godot;

namespace BodyCheck.Core.Controllers;

public sealed class PlatformController : ICharacterController<SimpleControlledEntity>
{
    private readonly float _startHeight;
    private float _move = 1;

    /// <param name="startHeight">The height of the platform's center that it tries to hold.</param>
    public PlatformController(float startHeight)
    {
        _startHeight = startHeight;
    }

    public void Apply(SimpleControlledEntity entity, float dt)
    {
        // Patrol. Bounce off of anything solid (including the player).
        if (_move < 0 && entity.Contacts.HasFlag(Contacts.Left))
        {
            _move = 1;
        }
        else if (_move > 0 && entity.Contacts.HasFlag(Contacts.Right))
        {
            _move = -1;
        }

        ApplyHorizontalThrust(entity, _move);
        ApplyVerticalThrust(entity);
    }

    private void ApplyHorizontalThrust(SimpleControlledEntity entity, float move)
    {
        Features features = Features.Current;
        float speed = entity.LinearVelocity.X;
        // The drive fades out as the entity nears top speed in the input direction.
        // It never slows an entity that is already faster than that.
        float headroom = Math.Clamp(1 - speed * MathF.Sign(move) / features.PlatformTopSpeed, 0, 1);

        entity.ApplyCentralForce(new Vector3(move * features.PlatformHorizontalDriveForce * headroom, 0, 0));
    }

    private void ApplyVerticalThrust(SimpleControlledEntity entity)
    {
        // PD controller: correction = K_p * (target - current) + K_d * -rate
        Features features = Features.Current;
        float weight = entity.Weight;
        var damping = 2 * MathF.Sqrt(features.PlatformVerticalStiffness * entity.Mass);
        var correction = features.PlatformVerticalStiffness * (_startHeight - entity.Position.Y) + damping * -entity.LinearVelocity.Y;

        var thrust = Math.Clamp(weight + correction, 0, weight * features.PlatformMaxVerticalThrustFrac);
        entity.ApplyCentralForce(new Vector3(0, thrust, 0));
    }
}
