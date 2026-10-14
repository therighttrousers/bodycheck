namespace BodyCheck.Core;

/// <summary>
/// What decides an entity's actions: the player's input, or an NPC's behaviour. It acts on the entity only
/// with forces, impulses and traction.
/// </summary>
public interface ICharacterController
{
    /// <summary>Called once per physics tick, before the engine's step.</summary>
    public void Apply(Entity entity, float dt);
}
