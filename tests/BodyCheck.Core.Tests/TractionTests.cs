using System.Numerics;

namespace BodyCheck.Core.Tests;

public class TractionTests
{
    private const float Dt = 1f / 60;
    private static readonly Vector2 HalfSize = new(0.5f, 0.5f);

    // An empty room 200 wide with its floor top at y = 1.
    private readonly World _world = new(TileGrid.FromText(
        Enumerable.Repeat(new string('.', 200), 10).Append(new string('#', 200)).ToList()));

    private Body AddBody(float x, float y, float mass = 1)
    {
        var body = new Body(new Vector2(x, y), HalfSize, mass);
        _world.Bodies.Add(body);
        return body;
    }

    /// <summary>Adds a body resting on the floor and another resting on top of it.</summary>
    private (Body Lower, Body Upper) AddStack()
    {
        Body lower = AddBody(100, 1.5f);
        Body upper = AddBody(100, 2.5f);
        Step(2);
        Assert.Same(lower, upper.Support);
        return (lower, upper);
    }

    private void Step(int ticks, Body? pushed = null, float force = 0)
    {
        for (int i = 0; i < ticks; i++)
        {
            pushed?.ApplyForce(new Vector2(force, 0));
            _world.Step(Dt);
        }
    }

    [Fact]
    public void A_body_without_traction_slides_freely()
    {
        Body body = AddBody(100, 1.5f);
        body.ApplyImpulse(new Vector2(3, 0));

        Step(60);

        Assert.Equal(3, body.Velocity.X);
    }

    [Fact]
    public void Braking_stops_a_body_without_reversing_it()
    {
        Body body = AddBody(100, 1.5f);
        body.Traction = new Traction(TargetSpeed: 0, MaxForce: 100);
        Step(1);
        body.ApplyImpulse(new Vector2(3, 0));

        Step(30);

        Assert.Equal(0, body.Velocity.X);
    }

    [Fact]
    public void Braking_holds_a_body_still_against_a_weaker_force()
    {
        Body body = AddBody(100, 1.5f);
        body.Traction = new Traction(TargetSpeed: 0, MaxForce: 100);
        Step(1);

        Step(60, pushed: body, force: 99);

        Assert.Equal(0, body.Velocity.X);
        Assert.Equal(100, body.Position.X);
    }

    [Fact]
    public void A_stronger_force_overcomes_braking_by_the_difference()
    {
        Body body = AddBody(100, 1.5f);
        body.Traction = new Traction(TargetSpeed: 0, MaxForce: 100);
        Step(1);

        Step(60, pushed: body, force: 130);

        Assert.Equal(30, body.Velocity.X, 3);
    }

    [Fact]
    public void Traction_accelerates_at_its_maximum_force_then_holds_the_target_speed()
    {
        Body body = AddBody(100, 1.5f, mass: 2);
        body.Traction = new Traction(TargetSpeed: 5, MaxForce: 120);
        Step(1);

        Step(1);
        Assert.Equal(1, body.Velocity.X, 4);

        Step(60);
        Assert.Equal(5, body.Velocity.X, 4);
    }

    [Fact]
    public void Traction_does_nothing_in_the_air()
    {
        Body body = AddBody(100, 8);
        body.Traction = new Traction(TargetSpeed: 5, MaxForce: 120);

        Step(5);

        Assert.Equal(0, body.Velocity.X);
    }

    [Fact]
    public void Traction_pushes_the_support_the_other_way()
    {
        (Body lower, Body upper) = AddStack();
        upper.Traction = new Traction(TargetSpeed: 10, MaxForce: 60);

        Step(1);

        Assert.Equal(1, upper.Velocity.X, 4);
        Assert.Equal(-1, lower.Velocity.X, 4);
    }

    [Fact]
    public void A_support_that_brakes_harder_than_it_is_pushed_stays_still()
    {
        (Body lower, Body upper) = AddStack();
        lower.Traction = new Traction(TargetSpeed: 0, MaxForce: 200);
        upper.Traction = new Traction(TargetSpeed: 10, MaxForce: 60);

        Step(30);

        // Stacks settle over several passes, so the lower body is only nearly still.
        Assert.Equal(0, lower.Velocity.X, 3);
        Assert.Equal(100, lower.Position.X, 3);
        Assert.Equal(10, upper.Velocity.X, 3);
    }

    [Fact]
    public void A_braking_body_is_carried_by_its_support_without_slipping()
    {
        (Body lower, Body upper) = AddStack();
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
