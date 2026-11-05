using System.IO;
using System.Linq;

using BodyCheck.Core.Levels;
using BodyCheck.Core.Simulation;
using BodyCheck.Core.WorldData;

using GdUnit4;

using Godot;

using static GdUnit4.Assertions;

namespace BodyCheck.Tests;

/// <summary>The game's own worlds, built as they are played.</summary>
[TestSuite]
[RequireGodotRuntime]
public class GameWorldsTest
{
    [TestCase]
    public void Builds_each_world_with_routes_that_its_triggers_can_run()
    {
        string path = ProjectSettings.GlobalizePath("res://levels/main.ldtk");
        foreach (World world in LdtkReader.ReadWorlds(path, File.ReadAllText))
        {
            var node = new WorldNode(world, new AssetLibrary());
            try
            {
                AssertArray(node.Triggers.Keys).ContainsExactlyInAnyOrder(
                    world.Controls.OfType<EntityConfig.Trigger>().Select(trigger => trigger.Id));
                foreach (var trigger in node.Triggers.Values)
                {
                    trigger.Touch(node.Tick);
                }
            }
            finally
            {
                RoomRun.FreeAndFinalize(node);
            }
        }
    }
}
