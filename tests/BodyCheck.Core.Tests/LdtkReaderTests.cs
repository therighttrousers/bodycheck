using BodyCheck.Core.Levels;
using BodyCheck.Core.WorldData;

using Godot;

namespace BodyCheck.Core.Tests;

public class LdtkReaderTests
{
    /// <summary>
    /// A project file with multi-worlds on, as the only file there is to read, with a world named Test of these levels.
    /// </summary>
    private static Func<string, string> Project(string levels) => Project([("Test", levels)]);

    /// <summary>A project file with multi-worlds on, as the only file there is to read, with these worlds.</summary>
    private static Func<string, string> Project((string Name, string Levels)[] worlds) =>
        ProjectFile(string.Join(", ", worlds.Select(world =>
            $$"""{ "identifier": "{{world.Name}}", "levels": [ {{world.Levels}} ] }""")), levels: "");

    private static Func<string, string> ProjectFile(string worlds, string levels) =>
        new Dictionary<string, string>
        {
            ["test.ldtk"] = $$"""{ "jsonVersion": "1.5.3", "worlds": [ {{worlds}} ], "levels": [ {{levels}} ] }""",
        }.GetValueOrDefault!;

    private static World ReadOne(Func<string, string> readFile) =>
        Assert.Single(LdtkReader.ReadWorlds("test.ldtk", readFile));

    /// <summary>
    /// A level of <paramref name="width"/> by <paramref name="height"/> tiles, with its layers in the project file. Its
    /// tiles are empty except for <paramref name="solid"/>, indexes into its IntGrid, top row first.
    /// </summary>
    /// <param name="pxWid">The level's width in pixels, if it isn't its layers' width.</param>
    private static string Level(
        int worldX, int worldY, string entities = "", int[]? solid = null, int width = 20, int height = 15,
        int? pxWid = null, int worldDepth = 0, string identifier = "Test")
    {
        int[] values = new int[width * height];
        foreach (int i in solid ?? [])
        {
            values[i] = 1;
        }
        return $$"""
            { "identifier": "{{identifier}}", "pxWid": {{pxWid ?? width * 16}}, "pxHei": {{height * 16}}, "worldX": {{worldX}}, "worldY": {{worldY}},
              "worldDepth": {{worldDepth}}, "externalRelPath": null, "layerInstances": [
                { "__identifier": "SolidGeometry", "__type": "IntGrid", "__cWid": {{width}}, "__cHei": {{height}}, "__gridSize": 16,
                  "__pxTotalOffsetX": 0, "__pxTotalOffsetY": 0,
                  "intGridCsv": [{{string.Join(", ", values)}}], "entityInstances": [] },
                { "__identifier": "Characters", "__type": "Entities", "__cWid": {{width}}, "__cHei": {{height}}, "__gridSize": 16,
                  "__pxTotalOffsetX": 0, "__pxTotalOffsetY": 0,
                  "intGridCsv": [], "entityInstances": [ {{entities}} ] },
                { "__identifier": "ControlEntities", "__type": "Entities", "__cWid": {{width}}, "__cHei": {{height}}, "__gridSize": 16,
                  "__pxTotalOffsetX": 0, "__pxTotalOffsetY": 0,
                  "intGridCsv": [], "entityInstances": [] } ] }
            """;
    }

    /// <summary>A player start in the second tile of a level's top row.</summary>
    private const string PlayerStart =
        """{ "__identifier": "PlayerStart", "__pivot": [0, 0], "px": [16, 0], "width": 16, "height": 16, "__smartColor": "#2CE8F5" }""";

    [Fact]
    public void Reads_the_game_worlds()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "levels", "main.ldtk");

        Assert.NotEmpty(LdtkReader.ReadWorlds(path, File.ReadAllText));
    }

    [Fact]
    public void Reads_each_world_named_after_it()
    {
        IReadOnlyList<World> worlds = LdtkReader.ReadWorlds("test.ldtk", Project(
            [("First", Level(0, 0, PlayerStart)), ("Second", Level(320, 0, PlayerStart))]));

        Assert.Equal(["First", "Second"], worlds.Select(world => world.Name));
        Assert.Equal(Vector2I.Zero, worlds[1].Rooms.Single().Origin);
    }

    [Fact]
    public void Reads_each_level_into_a_room_at_its_place_on_the_grid()
    {
        // One level up and to the left of the other, so that the world is 2x2 levels, two of them missing.
        const string robot =
            """{ "__identifier": "RobotBasic", "__pivot": [0.5, 1], "px": [8, 240], "width": 16, "height": 16, "__smartColor": "#FEAE34" }""";
        World world = ReadOne(Project(
            Level(-320, 0, robot, solid: [0], identifier: "Upper") + ", " + Level(0, 240, PlayerStart, identifier: "Lower")));

        Assert.Equal(["Upper", "Lower"], world.Rooms.Select(room => room.Name));
        Room upper = world.Rooms[0];
        Room lower = world.Rooms[1];

        Assert.Equal(new Vector2I(0, 15), upper.Origin);
        Assert.True(upper.IsSolid(0, 14)); // Its top left tile
        Assert.False(upper.IsSolid(1, 14));
        EntityConfig robotConfig = Assert.Single(upper.EntityConfigs);
        Assert.Equal((EntityKind.Robot, new Vector2(0.5f, 0)), (robotConfig.Kind, robotConfig.Start));

        Assert.Equal(new Vector2I(20, 0), lower.Origin);
        EntityConfig playerConfig = Assert.Single(lower.EntityConfigs);
        Assert.Equal((EntityKind.Player, new Vector2(1.5f, 14)), (playerConfig.Kind, playerConfig.Start));
    }

    [Fact]
    public void Reads_levels_of_any_size_anywhere_on_whole_tiles()
    {
        // A big level, and a small one beside it, a tile to the right and three tiles down from its top.
        World world = ReadOne(Project(
            Level(0, 0, width: 40, height: 30, solid: [40 * 29], identifier: "Big") + ", "
            + Level(656, 48, PlayerStart, width: 7, height: 5, identifier: "Small")));

        Room big = world.Rooms[0];
        Room small = world.Rooms[1];
        Assert.Equal((Vector2I.Zero, new Vector2I(40, 30)), (big.Origin, big.Size));
        Assert.True(big.IsSolid(0, 0)); // Its bottom left tile
        Assert.Equal((new Vector2I(41, 22), new Vector2I(7, 5)), (small.Origin, small.Size));
        Assert.Equal(new Vector2(1.5f, 4), Assert.Single(small.EntityConfigs).Start);
    }

    [Theory]
    [InlineData("""{ "__identifier": "Dragon", "__pivot": [0, 0], "px": [16, 0], "width": 16, "height": 16 }""")]
    [InlineData("""{ "__identifier": "PlayerStart", "__pivot": [0, 0], "px": [16, 0], "width": 16 }""")]
    [InlineData("""{ "__identifier": "PlayerStart", "__pivot": [0, 0], "px": null, "width": 16, "height": 16 }""")]
    [InlineData("""{ "__identifier": 0, "__pivot": [0, 0], "px": [16, 0], "width": 16, "height": 16 }""")]
    [InlineData("")] // No player
    public void Rejects_entity(string entity)
    {
        var e = Assert.Throws<InvalidDataException>(() => ReadOne(Project(Level(0, 0, entity))));
        Assert.StartsWith("test.ldtk: ", e.Message);
    }

    public static TheoryData<string> BadLayouts => new()
    {
        Level(0, 0, PlayerStart, pxWid: 336), // Wider than its layers
        Level(0, 0, PlayerStart, pxWid: 328), // Not a whole number of tiles
        Level(8, 0, PlayerStart), // Not a whole number of tiles from the origin
        Level(0, 0, PlayerStart, worldDepth: 1),
        Level(0, 0, PlayerStart) + ", " + Level(0, 0),
        Level(0, 0, PlayerStart) + ", " + Level(304, 224), // Overlapping by a tile
        Level(0, 0, PlayerStart) + ", " + Level(320, 0, PlayerStart),
    };

    [Theory]
    [MemberData(nameof(BadLayouts))]
    public void Rejects_layout(string levels)
    {
        var e = Assert.Throws<InvalidDataException>(() => ReadOne(Project(levels)));
        Assert.StartsWith("test.ldtk: ", e.Message);
    }

    [Fact]
    public void Rejects_a_project_without_multi_worlds()
    {
        Assert.Throws<InvalidDataException>(
            () => LdtkReader.ReadWorlds("test.ldtk", ProjectFile(worlds: "", levels: Level(0, 0, PlayerStart))));
    }

    [Fact]
    public void Rejects_a_world_without_levels()
    {
        Assert.Throws<InvalidDataException>(() => ReadOne(Project("")));
    }
}
