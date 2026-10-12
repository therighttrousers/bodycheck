namespace BodyCheck.Spike;

public interface IJoltCharacterController
{
    public JoltBody Body { get; }
    public void Apply(JoltWorld world, float dt);
}
