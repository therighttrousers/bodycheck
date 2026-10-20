using System;
using System.Threading.Tasks;

using BodyCheck.Core.Controllers;
using BodyCheck.Core.Entities;
using BodyCheck.Core.Simulation;
using BodyCheck.Core.WorldData;

using Godot;

namespace BodyCheck.Game.Tests;

/// <summary>
/// One room, as a world of one room, loaded into the running engine for a test, with the player's input under the test's control.
/// Free it when the test ends.
/// </summary>
public sealed class RoomRun
{
    public static readonly PlayerInput Idle = new(0, 0, false);
    public static readonly PlayerInput Right = new(1, 0, false);
    public static readonly PlayerInput Jump = new(0, 0, true);

    private PlayerInput _input = Idle;

    public RoomRun(Room room, BodyTuning? playerBody = null, bool controllersEnabled = true)
    {
        Node = new WorldNode(new World(room), EntityVisuals.For, playerBody: playerBody)
        {
            PlayerInput = () => _input,
            ControllersEnabled = controllersEnabled,
        };
        Tree.Root.AddChild(Node);
    }

    public WorldNode Node { get; }

    public Entity Player => Node.Player;

    private static SceneTree Tree => (SceneTree)Engine.GetMainLoop();

    /// <summary>A room from text rows, top row first, with the name "Test".</summary>
    public static Room RoomOf(params string[] rows) => Room.FromText("Test", string.Join('\n', rows));

    /// <summary>Frees the room, with <see cref="FreeAndFinalize"/>.</summary>
    public void Free() => FreeAndFinalize(Node);

    /// <summary>
    /// Frees a node, and runs the finalizers of the C# objects it leaves behind, such as the wrappers of its meshes and
    /// shapes. Godot crashes if a finalizer runs while it shuts down, so none may be left when the last test ends: every
    /// test that frees a world frees it with this.
    /// </summary>
    public static void FreeAndFinalize(Node node)
    {
        node.Free();
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    /// <summary>Runs the room for a number of physics ticks with the given input.</summary>
    /// <param name="eachTick">Called after each tick.</param>
    public async Task Tick(PlayerInput input, int ticks = 1, Action? eachTick = null)
    {
        _input = input;
        for (int i = 0; i < ticks; i++)
        {
            await Tree.ToSignal(Tree, SceneTree.SignalName.PhysicsFrame);
            eachTick?.Invoke();
        }
    }

    /// <summary>
    /// Runs the room's controllers for a number of physics ticks with the given input, as the game does while it is
    /// paused because a controller asked. Pause the tree first.
    /// </summary>
    public async Task PausedTick(PlayerInput input, int ticks = 1)
    {
        _input = input;
        for (int i = 0; i < ticks; i++)
        {
            await Tree.ToSignal(Tree, SceneTree.SignalName.PhysicsFrame);
            Node.StepPaused(1f / Engine.PhysicsTicksPerSecond);
        }
    }
}

public static class EntityExtensions
{
    public static float Left(this Entity node) => node.Position.X - node.HalfSize.X;
    public static float Right(this Entity node) => node.Position.X + node.HalfSize.X;
    public static float Bottom(this Entity node) => node.Position.Y - node.HalfSize.Y;
    public static float Top(this Entity node) => node.Position.Y + node.HalfSize.Y;
    public static float MomentumX(this Entity node) => node.Mass * node.LinearVelocity.X;
}
