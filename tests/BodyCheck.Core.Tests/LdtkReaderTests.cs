using Godot;

namespace BodyCheck.Core.Tests;

public class LdtkReaderTests
{
    /// <summary>A project file, as the only file there is to read.</summary>
    private static Func<string, string> Project(string levels, string worlds = "[]") =>
        new Dictionary<string, string>
        {
            ["test.ldtk"] = $$"""{ "jsonVersion": "1.5.3", "worlds": {{worlds}}, "levels": [ {{levels}} ] }""",
        }.GetValueOrDefault!;

    /// <summary>
    /// A level of 20x15 tiles, with its layers in the project file. Its tiles are empty except for
    /// <paramref name="solid"/>, indexes into its IntGrid, top row first.
    /// </summary>
    private static string Level(
        int worldX, int worldY, string entities = "", int[]? solid = null, int pxWid = 320, int worldDepth = 0)
    {
        int[] values = new int[20 * 15];
        foreach (int i in solid ?? [])
        {
            values[i] = 1;
        }
        return $$"""
            { "identifier": "Test", "pxWid": {{pxWid}}, "pxHei": 240, "worldX": {{worldX}}, "worldY": {{worldY}},
              "worldDepth": {{worldDepth}}, "externalRelPath": null, "layerInstances": [
                { "__identifier": "SolidGeometry", "__type": "IntGrid", "__cWid": 20, "__cHei": 15, "__gridSize": 16,
                  "__pxTotalOffsetX": 0, "__pxTotalOffsetY": 0,
                  "intGridCsv": [{{string.Join(", ", values)}}], "entityInstances": [] },
                { "__identifier": "Characters", "__type": "Entities", "__cWid": 20, "__cHei": 15, "__gridSize": 16,
                  "__pxTotalOffsetX": 0, "__pxTotalOffsetY": 0,
                  "intGridCsv": [], "entityInstances": [ {{entities}} ] } ] }
            """;
    }

    /// <summary>A player start in the second tile of a level's top row.</summary>
    private const string PlayerStart =
        """{ "__identifier": "PlayerStart", "__pivot": [0, 0], "px": [16, 0], "width": 16, "height": 16 }""";

    [Fact]
    public void Reads_the_game_world()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "levels", "main.ldtk");

        Assert.Equal("main", LdtkReader.ReadWorld(path, File.ReadAllText).Name);
    }

    [Fact]
    public void Stitches_levels_on_the_grid_and_fills_the_gaps()
    {
        // One level up and to the left of the other, so that the world is 2x2 levels, two of them missing.
        const string robot =
            """{ "__identifier": "RobotBasic", "__pivot": [0.5, 1], "px": [8, 240], "width": 16, "height": 16 }""";
        Room room = LdtkReader.ReadWorld("test.ldtk", Project(
            Level(-320, 0, robot, solid: [0]) + ", " + Level(0, 240, PlayerStart)));
        TileGrid tiles = room.CreateTiles();

        Assert.Equal((40, 30), (tiles.Width, tiles.Height));
        Assert.True(tiles.IsSolid(0, 29)); // The top left level's top left tile
        Assert.False(tiles.IsSolid(1, 29));
        Assert.False(tiles.IsSolid(5, 20));
        Assert.False(tiles.IsSolid(25, 5));
        Assert.True(tiles.IsSolid(25, 20)); // No level
        Assert.True(tiles.IsSolid(5, 5)); // No level
        Assert.Equal(new Vector2(21.5f, 14), room.PlayerStart);
        Assert.Equal(new Vector2(0.5f, 15), Assert.Single(room.RobotStarts));
    }

    [Theory]
    [InlineData("""{ "__identifier": "Dragon", "__pivot": [0, 0], "px": [16, 0], "width": 16, "height": 16 }""")]
    [InlineData("""{ "__identifier": "PlayerStart", "__pivot": [0, 0], "px": [16, 0], "width": 16 }""")]
    [InlineData("""{ "__identifier": "PlayerStart", "__pivot": [0, 0], "px": null, "width": 16, "height": 16 }""")]
    [InlineData("""{ "__identifier": 0, "__pivot": [0, 0], "px": [16, 0], "width": 16, "height": 16 }""")]
    [InlineData("")] // No player
    public void Rejects_entity(string entity)
    {
        var e = Assert.Throws<InvalidDataException>(() => LdtkReader.ReadWorld("test.ldtk", Project(Level(0, 0, entity))));
        Assert.StartsWith("test.ldtk: ", e.Message);
    }

    public static TheoryData<string> BadLayouts => new()
    {
        Level(0, 0, PlayerStart, pxWid: 336),
        Level(16, 0, PlayerStart),
        Level(0, 0, PlayerStart, worldDepth: 1),
        Level(0, 0, PlayerStart) + ", " + Level(0, 0),
        Level(0, 0, PlayerStart) + ", " + Level(320, 0, PlayerStart),
    };

    [Theory]
    [MemberData(nameof(BadLayouts))]
    public void Rejects_layout(string levels)
    {
        var e = Assert.Throws<InvalidDataException>(() => LdtkReader.ReadWorld("test.ldtk", Project(levels)));
        Assert.StartsWith("test.ldtk: ", e.Message);
    }

    [Fact]
    public void Rejects_several_worlds()
    {
        Assert.Throws<InvalidDataException>(() => LdtkReader.ReadWorld("test.ldtk", Project("", worlds: "[{}]")));
    }
}
