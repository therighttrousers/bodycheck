using System.Numerics;

namespace BodyCheck.Core.Tests;

public class BodyCollisionTests
{
    private const float Dt = 1f / 60;
    private static readonly Vector2 HalfSize = new(0.5f, 0.5f);

    // An empty room 20 wide with its floor top at y = 1.
    private readonly World _world = new(TileGrid.FromText(
        Enumerable.Repeat(new string('.', 20), 10).Append(new string('#', 20)).ToList()));

    private Body AddBody(float x, float y, float mass = 1)
    {
        var body = new Body(new Vector2(x, y), HalfSize, mass);
        _world.Bodies.Add(body);
        return body;
    }

    private void Step(int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            _world.Step(Dt);
        }
    }

    private static bool Overlap(Body a, Body b)
    {
        Vector2 distance = Vector2.Abs(a.Position - b.Position);
        Vector2 reach = a.HalfSize + b.HalfSize;
        return distance.X < reach.X - 1e-3f && distance.Y < reach.Y - 1e-3f;
    }

    [Fact]
    public void A_body_lands_on_another_and_stands_on_it()
    {
        Body lower = AddBody(5, 1.5f);
        Body upper = AddBody(5.3f, 5);

        Step(120);

        Assert.Equal(2.5f, upper.Position.Y, 3);
        Assert.Equal(1.5f, lower.Position.Y, 3);
        Assert.True(upper.OnGround);
        Assert.Same(lower, upper.Support);
        Assert.Null(lower.Support);
    }

    [Fact]
    public void Standing_works_whichever_body_moves_first()
    {
        Body upper = AddBody(5.3f, 5);
        Body lower = AddBody(5, 1.5f);

        Step(120);

        Assert.Equal(2.5f, upper.Position.Y, 3);
        Assert.Same(lower, upper.Support);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 3)]
    [InlineData(3, 1)]
    public void Colliding_bodies_share_their_momentum_without_bouncing(float movingMass, float restingMass)
    {
        _world.Gravity = Vector2.Zero;
        Body moving = AddBody(5, 5, movingMass);
        Body resting = AddBody(8, 5, restingMass);
        moving.ApplyImpulse(new Vector2(6 * movingMass, 0));

        Step(60);

        float shared = 6 * movingMass / (movingMass + restingMass);
        Assert.Equal(shared, moving.Velocity.X, 4);
        Assert.Equal(shared, resting.Velocity.X, 4);
        Assert.False(Overlap(moving, resting));
    }

    [Fact]
    public void A_body_is_not_pushed_through_a_wall()
    {
        _world.Gravity = Vector2.Zero;
        Body pushed = AddBody(18, 5);
        Body pusher = AddBody(16, 5);

        for (int i = 0; i < 120; i++)
        {
            pusher.ApplyForce(new Vector2(50, 0));
            _world.Step(Dt);
        }

        Assert.Equal(19.5f, pushed.Position.X, 3);
        Assert.Equal(18.5f, pusher.Position.X, 3);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_rising_body_lifts_the_one_standing_on_it(bool lowerMovesFirst)
    {
        Body lower, upper;
        if (lowerMovesFirst)
        {
            lower = AddBody(5, 1.5f);
            upper = AddBody(5, 2.5f);
        }
        else
        {
            upper = AddBody(5, 2.5f);
            lower = AddBody(5, 1.5f);
        }
        float weight = -_world.Gravity.Y * (lower.Mass + upper.Mass);

        for (int i = 0; i < 60; i++)
        {
            lower.ApplyForce(new Vector2(0, 1.5f * weight));
            _world.Step(Dt);
            Assert.False(Overlap(lower, upper));
        }

        Assert.True(lower.Position.Y > 4);
        Assert.InRange(upper.Position.Y - lower.Position.Y, 1, 1.2f);
        Assert.Same(lower, upper.Support);
    }

    [Fact]
    public void Bodies_side_by_side_on_the_floor_do_not_block_each_other_from_above()
    {
        Body left = AddBody(5, 1.5f);
        Body right = AddBody(6, 1.5f);
        Body top = AddBody(5.5f, 2.5f);

        Step(60);

        Assert.Equal(2.5f, top.Position.Y, 3);
        Assert.Equal(5.5f, top.Position.X, 3);
        Assert.Equal(5, left.Position.X, 3);
        Assert.Equal(6, right.Position.X, 3);
    }
}
