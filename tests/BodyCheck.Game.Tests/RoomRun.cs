using System;
using System.Threading.Tasks;

using BodyCheck.Core;

using Godot;

namespace BodyCheck.Game.Tests;

/// <summary>
/// One room loaded into the running engine for a test, with the player's input under the test's control.
/// Free it when the test ends.
/// </summary>
public sealed class RoomRun
{
    public static readonly PlayerInput Idle = new(0, false);
    public static readonly PlayerInput Right = new(1, false);
    public static readonly PlayerInput Jump = new(0, true);

    private PlayerInput _input = Idle;

    public RoomRun(Room room, PlayerTuning? playerTuning = null, bool controllersEnabled = true)
    {
        Node = new RoomNode(room, playerController: new PlayerController(playerTuning ?? new PlayerTuning()))
        {
            PlayerInput = () => _input,
            ControllersEnabled = controllersEnabled,
        };
        Tree.Root.AddChild(Node);
    }

    public RoomNode Node { get; }

    public BodyNode Player => Node.Player;

    private static SceneTree Tree => (SceneTree)Engine.GetMainLoop();

    /// <summary>A room from text rows, top row first, with the name "Test".</summary>
    public static Room RoomOf(params string[] rows) => new("Test", string.Join('\n', rows));

    public void Free() => Node.Free();

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
}

public static class BodyNodeExtensions
{
    // These read the engine's own state. A Body's copy is a tick older at the points where a test looks.

    public static float Left(this BodyNode node) => node.Position.X - node.Body.HalfSize.X;
    public static float Right(this BodyNode node) => node.Position.X + node.Body.HalfSize.X;
    public static float Bottom(this BodyNode node) => node.Position.Y - node.Body.HalfSize.Y;
    public static float Top(this BodyNode node) => node.Position.Y + node.Body.HalfSize.Y;
    public static float MomentumX(this BodyNode node) => node.Mass * node.LinearVelocity.X;
}
