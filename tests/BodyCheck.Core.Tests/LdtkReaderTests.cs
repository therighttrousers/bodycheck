using Godot;

namespace BodyCheck.Core.Tests;

public class LdtkReaderTests
{
    /// <summary>A project with one level in it, 3 tiles by 2, whose characters layer holds <paramref name="entity"/>.</summary>
    private static Dictionary<string, string> Project(string entity) => new()
    {
        ["test.ldtk"] = """
            { "jsonVersion": "1.5.3", "levels": [
                { "identifier": "Test", "pxWid": 48, "pxHei": 32, "externalRelPath": null, "layerInstances": [
                    { "__identifier": "SolidGeometry", "__type": "IntGrid", "__cWid": 3, "__cHei": 2, "__gridSize": 16,
                      "__pxTotalOffsetX": 0, "__pxTotalOffsetY": 0,
                      "intGridCsv": [0, 0, 0, 1, 2, 1], "entityInstances": [] },
                    { "__identifier": "Characters", "__type": "Entities", "__cWid": 3, "__cHei": 2, "__gridSize": 16,
                      "__pxTotalOffsetX": 0, "__pxTotalOffsetY": 0,
                      "intGridCsv": [], "entityInstances": [ ENTITY ] } ] } ] }
            """.Replace("ENTITY", entity),
    };

    private const string PlayerStart =
        """{ "__identifier": "PlayerStart", "__pivot": [0, 0], "px": [16, 0], "width": 16, "height": 16 }""";

    [Fact]
    public void Reads_every_level_of_the_game()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "levels", "main.ldtk");

        Assert.NotEmpty(LdtkReader.ReadRooms(path, File.ReadAllText));
    }

    [Fact]
    public void Puts_the_top_row_at_the_top_and_entities_on_the_bottoms_of_their_boxes()
    {
        Room room = Assert.Single(LdtkReader.ReadRooms("test.ldtk", Project(PlayerStart).GetValueOrDefault!));
        TileGrid tiles = room.CreateTiles();

        Assert.Equal("Test", room.Name);
        Assert.True(tiles.IsSolid(0, 0));
        Assert.True(tiles.IsSolid(1, 0));
        Assert.False(tiles.IsSolid(1, 1));
        Assert.Equal(new Vector2(1.5f, 1), room.PlayerStart);
    }

    [Theory]
    [InlineData("""{ "__identifier": "Dragon", "__pivot": [0, 0], "px": [16, 0], "width": 16, "height": 16 }""")]
    [InlineData("""{ "__identifier": "PlayerStart", "__pivot": [0, 0], "px": [16, 0], "width": 16 }""")]
    [InlineData("""{ "__identifier": "PlayerStart", "__pivot": [0, 0], "px": null, "width": 16, "height": 16 }""")]
    [InlineData("""{ "__identifier": 0, "__pivot": [0, 0], "px": [16, 0], "width": 16, "height": 16 }""")]
    [InlineData("")] // No player
    public void Rejects(string entity)
    {
        var e = Assert.Throws<InvalidDataException>(() => LdtkReader.ReadRooms("test.ldtk", Project(entity).GetValueOrDefault!));
        Assert.StartsWith("test.ldtk: ", e.Message);
    }
}
