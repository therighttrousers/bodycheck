using System.IO;
using System.Linq;
using System.Threading.Tasks;

using BodyCheck.Core.Levels;
using BodyCheck.Core.Simulation;
using BodyCheck.Core.WorldData;
using BodyCheck.Scripts;

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
    public async Task Builds_each_world_with_its_dialogues_scripts_and_routes_that_its_triggers_can_run()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        string path = ProjectSettings.GlobalizePath("res://levels/main.ldtk");
        var dialogue = new DialogueBox();
        try
        {
            foreach (World world in LdtkReader.ReadWorlds(path, File.ReadAllText))
            {
                // Building it checks that its dialogues name titles that their files hold, and its scripts scripts.
                var node = new WorldNode(world, new AssetLibrary(), dialogue: dialogue, scripts: new ScriptLibrary());
                try
                {
                    AssertArray(node.Triggers.Keys).ContainsExactlyInAnyOrder(
                        world.Controls.OfType<EntityConfig.Trigger>().Select(trigger => trigger.Id));

                    // In the tree and in a physics tick, since what a route runs may query the engine, as a door does.
                    tree.Root.AddChild(node);
                    await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
                    foreach (var trigger in node.Triggers.Values)
                    {
                        _ = trigger.Touch(node.Tick);
                    }
                }
                finally
                {
                    RoomRun.FreeAndFinalize(node);
                }
            }
        }
        finally
        {
            RoomRun.FreeAndFinalize(dialogue);
        }
    }
}
