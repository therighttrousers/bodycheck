using System.Numerics;

namespace BodyCheck.Core.Tests;

public class WorldTests
{
    private const float Dt = 1f / 60;
    private static readonly Vector2 HalfSize = new(0.4f, 0.45f);

    // Floor top is at y = 1. The wall's left face is at x = 10.
    private static World NewWorld() => new(TileGrid.FromText(
    [
        "..........#.........",
        "..........#.........",
        "..........#.........",
        "..........#.........",
        "####################",
    ]));

    private static Body AddBody(World world, float x, float y, float mass = 1)
    {
        var body = new Body(new Vector2(x, y), HalfSize, mass);
        world.Bodies.Add(body);
        return body;
    }

    private static void Step(World world, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            world.Step(Dt);
        }
    }

    [Fact]
    public void FromText_puts_the_last_row_at_the_bottom()
    {
        TileGrid grid = NewWorld().Tiles;

        Assert.True(grid.IsSolid(0, 0));
        Assert.False(grid.IsSolid(0, 1));
        Assert.True(grid.IsSolid(10, 4));
    }

    [Fact]
    public void Outside_the_grid_is_solid()
    {
        TileGrid grid = NewWorld().Tiles;

        Assert.True(grid.IsSolid(-1, 2));
        Assert.True(grid.IsSolid(20, 2));
        Assert.True(grid.IsSolid(5, 5));
    }

    [Fact]
    public void A_falling_body_comes_to_rest_on_the_floor()
    {
        World world = NewWorld();
        Body body = AddBody(world, 5, 4);

        Step(world, 120);

        Assert.Equal(1 + HalfSize.Y, body.Position.Y, 4);
        Assert.Equal(0, body.Velocity.Y);
        Assert.True(body.OnGround);
    }

    [Fact]
    public void A_body_stops_flush_against_a_wall()
    {
        World world = NewWorld();
        Body body = AddBody(world, 5, 1 + HalfSize.Y);
        body.ApplyImpulse(new Vector2(5, 0));

        Step(world, 120);

        Assert.Equal(10 - HalfSize.X, body.Position.X, 4);
        Assert.Equal(0, body.Velocity.X);
    }

    [Fact]
    public void A_very_fast_body_does_not_pass_through_a_wall()
    {
        World world = NewWorld();
        Body body = AddBody(world, 5, 3);
        body.ApplyImpulse(new Vector2(1000, 0));

        world.Step(Dt);

        Assert.Equal(10 - HalfSize.X, body.Position.X, 4);
        Assert.Equal(Contacts.Right, body.Contacts);
    }

    [Fact]
    public void A_body_sliding_along_the_floor_is_not_stopped_by_floor_tiles()
    {
        World world = NewWorld();
        Body body = AddBody(world, 2, 1 + HalfSize.Y);
        body.ApplyImpulse(new Vector2(3, 0));

        Step(world, 60);

        Assert.Equal(5, body.Position.X, 3);
        Assert.Equal(Contacts.Floor, body.Contacts);
    }

    [Fact]
    public void Drag_slows_a_moving_body()
    {
        World world = NewWorld();
        world.Gravity = Vector2.Zero;
        Body body = AddBody(world, 2, 3);
        body.Drag = 1;
        body.ApplyImpulse(new Vector2(3, 0));

        Step(world, 60);

        Assert.InRange(body.Velocity.X, 1.0f, 1.2f);
    }
}
