namespace BodyCheck.Core.Entities;

/// <summary>
/// What decides an entity's actions: the player's input, or an NPC's behaviour. It acts on the entity only
/// with forces, impulses and the motors that the entity's kind of body has.
/// <para>
/// <typeparamref name="TEntity"/> is the kind of body the controller needs. A controller that needs less
/// fits more: one written for any <see cref="Entity"/> can control every kind.
/// </para>
/// </summary>
public interface ICharacterController<in TEntity>
    where TEntity : Entity
{
    /// <summary>Called once per physics tick, before the engine's step.</summary>
    public void Apply(TEntity entity, float dt);

    /// <summary>
    /// Whether the controller asks for the game to be paused, so that it can decide something with all else
    /// standing still, such as which way to phase. Whatever pauses the game for it calls <see cref="ApplyPaused"/>
    /// instead of <see cref="Apply"/> until the controller stops asking.
    /// </summary>
    public bool RequestsPause => false;

    /// <summary>
    /// Called once per physics tick while the game is paused because a controller asked. Nothing moves then:
    /// the engine doesn't step, so forces and impulses wait until play resumes.
    /// </summary>
    public void ApplyPaused(TEntity entity, float dt)
    {
    }
}
