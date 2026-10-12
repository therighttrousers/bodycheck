namespace BodyCheck.Core;

/// <summary>
/// Rooms for tests and probes. They measure one thing each and aren't meant for playing.
/// </summary>
public static class TestRooms
{
    /// <summary>A long flat floor with a high ceiling, for sliding a body over many tile seams.</summary>
    public static readonly Room Seams = new("Seams", string.Join('\n',
    [
        new string('#', 64),
        .. Enumerable.Repeat("#" + new string('.', 62) + "#", 7),
        "#.P" + new string('.', 60) + "#",
        new string('#', 64),
    ]));

    /// <summary>A two-tile wall on a floor, for jumping beside.</summary>
    public static readonly Room Corner = new("Corner", """
        ............
        ............
        ............
        ............
        ............
        ............
        .........#..
        ..P......#..
        ############
        """);

    /// <summary>A long floor with the player above a robot, which has room to patrol one way for a long time.</summary>
    public static readonly Room RobotRiding = new("Robot riding", string.Join('\n',
    [
        .. Enumerable.Repeat(new string('.', 400), 8),
        new string('.', 20) + "P" + new string('.', 379),
        new string('.', 400),
        new string('.', 20) + "R" + new string('.', 379),
        new string('#', 400),
    ]));

    /// <summary>A hovering platform between two walls, with the player about 3 tiles above it.</summary>
    public static readonly Room PlatformRiding = new("Platform riding", string.Join('\n',
    [
        .. Enumerable.Repeat("#" + new string('.', 38) + "#", 3),
        "#" + new string('.', 19) + "P" + new string('.', 18) + "#",
        .. Enumerable.Repeat("#" + new string('.', 38) + "#", 3),
        "#" + new string('.', 19) + "H" + new string('.', 18) + "#",
        .. Enumerable.Repeat("#" + new string('.', 38) + "#", 6),
        new string('#', 40),
    ]));
}
