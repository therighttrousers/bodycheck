using Godot;

namespace BodyCheck.Core.Tests;

/// <summary>
/// <see cref="World.Step"/> without the physics engine. The tests stand in for the engine: they say what
/// each body stands on, and apply the impulse the step hands back. Nothing collides and nothing falls.
/// </summary>
public class TractionTests
{
    private const float Dt = 1f / 60;
    private static readonly Vector2 HalfSize = new(0.5f, 0.5f);

    private readonly World _world = new();

    /// <summary>Adds a body standing on <paramref name="support"/>, or on tiles if that is null.</summary>
    private Body AddGrounded(float mass = 1, Body? support = null)
    {
        Body body = AddAirborne(mass);
        body.SetContacts(Contacts.Floor, support);
        return body;
    }

    private Body AddAirborne(float mass = 1)
    {
        var body = new Body(HalfSize, mass);
        _world.Bodies.Add(body);
        return body;
    }

    private void Step(int ticks, Body? pushed = null, float force = 0)
    {
        for (int i = 0; i < ticks; i++)
        {
            pushed?.AddForce(new Vector2(force, 0));
            _world.Step(Dt);
            foreach (Body body in _world.Bodies)
            {
                Vector2 velocity = body.Velocity + body.TakeEngineImpulse() / body.Mass;
                body.SetMotion(body.Position + velocity * Dt, velocity);
            }
        }
    }

    [Fact]
    public void A_body_without_traction_slides_freely()
    {
        Body body = AddGrounded();
        body.AddImpulse(new Vector2(3, 0));

        Step(60);

        Assert.Equal(3, body.Velocity.X);
    }

    [Fact]
    public void Drag_slows_a_moving_body()
    {
        Body body = AddAirborne();
        body.Drag = 0.5f;
        body.AddImpulse(new Vector2(10, 0));

        Step(60);

        Assert.InRange(body.Velocity.X, 6.0f, 6.2f);
    }

    [Fact]
    public void Braking_stops_a_body_without_reversing_it()
    {
        Body body = AddGrounded();
        body.Traction = new Traction(TargetSpeed: 0, MaxForce: 100);
        body.AddImpulse(new Vector2(3, 0));

        Step(30);

        Assert.Equal(0, body.Velocity.X);
    }

    [Fact]
    public void Braking_holds_a_body_still_against_a_weaker_force()
    {
        Body body = AddGrounded();
        body.Traction = new Traction(TargetSpeed: 0, MaxForce: 100);

        Step(60, pushed: body, force: 99);

        Assert.Equal(0, body.Velocity.X);
        Assert.Equal(0, body.Position.X);
    }

    [Fact]
    public void A_stronger_force_overcomes_braking_by_the_difference()
    {
        Body body = AddGrounded();
        body.Traction = new Traction(TargetSpeed: 0, MaxForce: 100);

        Step(60, pushed: body, force: 130);

        Assert.Equal(30, body.Velocity.X, 3);
    }

    [Fact]
    public void Traction_accelerates_at_its_maximum_force_then_holds_the_target_speed()
    {
        Body body = AddGrounded(mass: 2);
        body.Traction = new Traction(TargetSpeed: 5, MaxForce: 120);

        Step(1);
        Assert.Equal(1, body.Velocity.X, 4);

        Step(60);
        Assert.Equal(5, body.Velocity.X, 4);
    }

    [Fact]
    public void Traction_does_nothing_in_the_air()
    {
        Body body = AddAirborne();
        body.Traction = new Traction(TargetSpeed: 5, MaxForce: 120);

        Step(5);

        Assert.Equal(0, body.Velocity.X);
    }

    [Fact]
    public void Traction_pushes_the_support_the_other_way()
    {
        Body lower = AddGrounded();
        Body upper = AddGrounded(support: lower);
        upper.Traction = new Traction(TargetSpeed: 10, MaxForce: 60);

        Step(1);

        Assert.Equal(1, upper.Velocity.X, 4);
        Assert.Equal(-1, lower.Velocity.X, 4);
    }

    [Fact]
    public void A_support_that_brakes_harder_than_it_is_pushed_stays_still()
    {
        Body lower = AddGrounded();
        Body upper = AddGrounded(support: lower);
        lower.Traction = new Traction(TargetSpeed: 0, MaxForce: 200);
        upper.Traction = new Traction(TargetSpeed: 10, MaxForce: 60);

        Step(30);

        // Stacks settle over several passes, so the lower body is only nearly still.
        Assert.Equal(0, lower.Velocity.X, 3);
        Assert.Equal(0, lower.Position.X, 3);
        Assert.Equal(10, upper.Velocity.X, 3);
    }

    [Fact]
    public void A_braking_body_is_carried_by_its_support_without_slipping()
    {
        Body lower = AddGrounded();
        Body upper = AddGrounded(support: lower);
        lower.Traction = new Traction(TargetSpeed: 3, MaxForce: 30);
        upper.Traction = new Traction(TargetSpeed: 0, MaxForce: 200);
        upper.Drag = 0.5f;

        Step(600);

        // Stacks settle over several passes, so the lower body is only nearly at its target.
        Assert.Equal(3, lower.Velocity.X, 2);
        Assert.Equal(lower.Velocity.X, upper.Velocity.X, 4);
        Assert.Equal(lower.Position.X, upper.Position.X, 3);
    }
}
