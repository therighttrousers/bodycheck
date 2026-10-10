namespace BodyCheck.Core;

public interface ICharacterController
{
    public Body Body { get; }
    public void Apply(World world, float dt);
}
