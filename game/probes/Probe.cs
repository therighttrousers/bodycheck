using System;
using System.Linq;

using BodyCheck.Core.Controllers;
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
    public void Tick(float dt);

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
                // Nothing may brake the sliding body, so the controllers are off.
                world.ControllersEnabled = false;
                GD.Print($"Seam probe: tiles {(mergeTiles ? "merged" : "single")}");
                probe = new SeamProbe(world.Player, speed: world.PlayerTuning.TopSpeed);
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
            default:
                throw new ArgumentException($"There is no probe named '{name}'.");
        }

        world.PlayerInput = () => probe.Input;
        return probe;
    }
}
