using System;

using BodyCheck.Core;

using Godot;

namespace BodyCheck.Probes;

/// <summary>
/// A scripted measurement of one room, which prints what it finds. <see cref="Main"/> runs one when given
/// "--probe=" and its name on the command line, and quits when it is done.
/// </summary>
public interface IProbe
{
    public bool Done { get; }

    /// <summary>Called once per physics tick, before the room's own tick.</summary>
    public void Tick(float dt);

    /// <summary>The player's input for this tick, for probes that drive the player.</summary>
    public PlayerInput Input => default;
}

public static class Probe
{
    /// <summary>Loads the room that the named probe needs, and returns the probe, attached to that room.</summary>
    /// <param name="load">Loads a room into the scene.</param>
    public static IProbe Start(string name, Func<Room, RoomNode> load, bool mergeTiles)
    {
        RoomNode room;
        IProbe probe;
        switch (name)
        {
            case "seams":
                room = load(TestRooms.Seams);
                // Nothing may brake the sliding body, so the controllers are off.
                room.ControllersEnabled = false;
                GD.Print($"Seam probe: tiles {(mergeTiles ? "merged" : "single")}");
                probe = new SeamProbe(room.Player, speed: room.PlayerController.Tuning.TopSpeed);
                break;
            case "player":
                room = load(TestRooms.Seams);
                GD.Print("Player probe:");
                probe = new PlayerProbe(room.Player);
                break;
            case "corner" or "corner-hold":
                room = load(TestRooms.Corner);
                GD.Print($"Corner probe, {(name == "corner" ? "not pushing" : "pushing")} during the jump:");
                probe = new CornerProbe(room.Player, holdRight: name == "corner-hold");
                break;
            case "drop":
                room = load(TestRooms.Corner);
                GD.Print("Drop probe:");
                probe = new DropProbe(room.Player);
                break;
            case "ride-robot" or "ride-platform":
                room = load(name == "ride-robot" ? TestRooms.RobotRiding : TestRooms.PlatformRiding);
                GD.Print($"Riding probe, {room.Room.Name}:");
                probe = new RidingProbe(room.Player, room.BodyNodes[1]);
                break;
            default:
                throw new ArgumentException($"There is no probe named '{name}'.");
        }

        room.PlayerInput = () => probe.Input;
        return probe;
    }
}
