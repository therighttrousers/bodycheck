using System;
using System.Linq;

using BodyCheck.Core;
using BodyCheck.Core.Controllers;
using BodyCheck.Core.Entities;
using BodyCheck.Core.Simulation;
using BodyCheck.Core.WorldData;

using Godot;

namespace BodyCheck.Probes;

/// <summary>
/// A scripted measurement of one world, which prints what it finds. <see cref="Main"/> runs one when given
/// "--probe=" and its name on the command line, and quits when it is done.
/// </summary>
public interface IProbe
{
    public bool Done { get; }

    /// <summary>Called once per physics tick, before the world's own tick.</summary>
    public void Tick(float gameDt);

    /// <summary>The player's input for this tick, for probes that drive the player.</summary>
    public PlayerInput Input => default;
}

public static class Probe
{
    /// <summary>
    /// Loads the world that the named probe needs, a world of one test room, and returns the probe, attached to that
    /// world.
    /// </summary>
    /// <param name="load">Loads a world into the scene.</param>
    public static IProbe Start(string name, Func<World, WorldNode> load, bool mergeTiles)
    {
        WorldNode Load(Room room) => load(new World(room));

        WorldNode world;
        IProbe probe;
        switch (name)
        {
            case "seams":
                world = Load(TestRoomLibrary.Seams);
                // Nothing may brake the sliding body, so the controllers and ground friction are off.
                world.ControllersEnabled = false;
                Features.Set(Features.Current with { PlayerGroundFriction = 0 });
                GD.Print($"Seam probe: tiles {(mergeTiles ? "merged" : "single")}");
                probe = new SeamProbe(world.Player, speed: Features.Current.PlayerTopSpeed);
                break;
            case "player":
                world = Load(TestRoomLibrary.Seams);
                GD.Print("Player probe:");
                probe = new PlayerProbe(world.Player);
                break;
            case "corner" or "corner-hold":
                world = Load(TestRoomLibrary.Corner);
                GD.Print($"Corner probe, {(name == "corner" ? "not pushing" : "pushing")} during the jump:");
                probe = new CornerProbe(world.Player, holdRight: name == "corner-hold");
                break;
            case "drop":
                world = Load(TestRoomLibrary.Corner);
                GD.Print("Drop probe:");
                probe = new DropProbe(world.Player);
                break;
            case "ride-robot" or "ride-platform":
                world = Load(name == "ride-robot" ? TestRoomLibrary.RobotRiding : TestRoomLibrary.PlatformRiding);
                GD.Print($"Riding probe, {world.World.Name}:");
                EntityKind carrier = name == "ride-robot" ? EntityKind.Robot : EntityKind.Platform;
                probe = new RidingProbe(world.Player, world.EntitiesOf(carrier).Single());
                break;
            case "landing":
                world = Load(TestRoomLibrary.Seams);
                GD.Print("Landing probe:");
                probe = new LandingProbe(world.Player);
                break;
            case "push":
                world = Load(TestRoomLibrary.RobotPush);
                GD.Print("Push probe:");
                probe = new PushProbe(world.Player, world.EntitiesOf(EntityKind.Robot).Single());
                break;
            case "run-off":
                world = Load(TestRoomLibrary.RobotRiding);
                GD.Print("Run-off probe:");
                probe = new RunOffProbe(world.Player, world.EntitiesOf(EntityKind.Robot).Single());
                break;
            case "carry":
                world = Load(TestRoomLibrary.Carrying);
                GD.Print("Carry probe:");
                {
                    var blocks = world.EntitiesOf(EntityKind.Block).ToList();
                    Entity carried = blocks.MaxBy(block => block.Position.Y)!;
                    Entity resting = blocks.MinBy(block => block.Position.Y)!;
                    Entity stacked = blocks.Single(block => block != carried && block != resting);
                    probe = new CarryProbe(world.Player, carried, resting, stacked);
                }
                break;
            case "wobble" or "offset-landing" or "tilted":
                world = Load(TestRoomLibrary.Carrying);
                GD.Print($"Wobble probe, {name}:");
                WobbleProbe.Case @case = name switch
                {
                    "wobble" => WobbleProbe.Case.Wobble,
                    "offset-landing" => WobbleProbe.Case.OffsetLanding,
                    _ => WobbleProbe.Case.Tilted,
                };
                probe = new WobbleProbe(world.Player, world.EntitiesOf(EntityKind.Block).MaxBy(block => block.Position.Y)!, @case);
                break;
            case "joint-placement":
                world = Load(TestRoomLibrary.FloatingBlock);
                GD.Print("Joint placement probe:");
                probe = new JointPlacementProbe(world.EntitiesOf(EntityKind.Block).Single(), world.Player);
                break;
            case "phase":
                world = Load(RoomLibrary.Phase);
                GD.Print("Phase probe:");
                probe = new PhaseProbe(world.Player, world.PlayerController, world.EntitiesOf(EntityKind.Robot).First());
                break;
            default:
                throw new ArgumentException($"There is no probe named '{name}'.");
        }

        world.PlayerInput = () => probe.Input;
        return probe;
    }
}
