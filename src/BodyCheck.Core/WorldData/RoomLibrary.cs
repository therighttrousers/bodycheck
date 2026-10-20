namespace BodyCheck.Core.WorldData;

/// <summary>
/// The library of test rooms. Design docs and tests refer to these by name.
/// </summary>
public static class RoomLibrary
{
    /// <summary>
    /// Things to phase through and throw: robots, blocks and a platform. A ledge on the left that only a phase
    /// up through something reaches, a pit to throw a robot into, a wall that a phase can't cross, and a pile of
    /// blocks on the right. Bumps keep the robots in their pens.
    /// </summary>
    public static readonly Room Phase = Room.FromText("Phase", """
        ######################################
        #....................................#
        #....................................#
        #....................................#
        #....................................#
        #....................................#
        #....................................#
        #....................................#
        #..................H.........#########
        #.................................####
        #####...........................B..###
        #..............................BB...##
        #..............................BB...##
        #.....................#.......BBBB...#
        #........#......#.....#.......BBBB...#
        #...R....#.P....#..R..#...R..BBBBBB..#
        #############...######################
        #############...######################
        ######################################
        """);

    /// <summary>
    /// A room the shape of the screen, for judging the rendering. The game adds turning shapes to its
    /// upper half.
    /// </summary>
    public static readonly Room Look = Room.FromText("Look", """
        ########################
        #......................#
        #......................#
        #......................#
        #......................#
        #......................#
        #......................#
        #......................#
        #......................#
        #......................#
        #......................#
        #......####............#
        #..................H...#
        #......................#
        #...........###........#
        #......................#
        #..P......R.......B....#
        ########################
        """);

    public static IReadOnlyList<Room> All { get; } = [Phase, Look];
}
