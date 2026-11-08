using System;
using System.Collections.Generic;
using System.Linq;

using BodyCheck.Core.Entities;
using BodyCheck.Core.Scripts;
using BodyCheck.Core.Tasks;

using Godot;

namespace BodyCheck.Scripts;

public class Test_NBodySimulation(IReadOnlyList<Entity> entities) : IScript
{
    private const float G = 25;

    // How far a body may get from where the bodies' center of mass started before it is removed, in tiles: about out of
    // the room.
    private const float Escape = 20;

    public async TickTask Run(IScriptWorld world)
    {
        foreach (var e in entities)
        {
            e.GravityScale = 0;
            e.PhysicsMaterialOverride = new PhysicsMaterial { Friction = 0 };
        }

        // Try to start them off in orbit around their center of mass. Give 'em the best chance to be orderly, see?
        var centerOfMass = entities.Aggregate(Vector3.Zero, (p, e) => p + e.Mass * e.Position) / entities.Sum(e => e.Mass);
        foreach (var e in entities)
        {
            Vector3 r = e.Position - centerOfMass;
            float pull = -ForceOn(e).Dot(r.Normalized()); // toward the center
            float speed = Mathf.Sqrt(Mathf.Max(0, pull * r.Length() / e.Mass));
            e.LinearVelocity = speed * Vector3.Back.Cross(r.Normalized());
        }
        var drift = entities.Aggregate(Vector3.Zero, (p, e) => p + e.Mass * e.LinearVelocity) / entities.Sum(e => e.Mass);
        foreach (var e in entities)
        {
            e.LinearVelocity -= drift;
        }

        // Until every body has gone.
        await world.Tick.While(
            () => entities.Any(e => e.IsLive()),
            () =>
            {
                foreach (var e in entities.Where(e => e.IsLive() && e.Position.DistanceTo(centerOfMass) > Escape))
                {
                    world.Remove(e);
                }
                entities = [.. entities.Where(e => e.IsLive())];
                foreach (var e in entities)
                {
                    e.ApplyCentralForce(ForceOn(e));
                }
            });
    }

    private Vector3 ForceOn(Entity entity)
    {
        var force = Vector3.Zero;
        foreach (var other in entities)
        {
            if (ReferenceEquals(entity, other)) continue;
            var dp = other.Position - entity.Position;
            force += G * entity.Mass * other.Mass / dp.LengthSquared() * dp.Normalized();
        }
        return force;
    }
}
