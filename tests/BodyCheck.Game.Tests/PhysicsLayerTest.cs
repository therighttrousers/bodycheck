using System;
using System.Linq;
using System.Numerics;

using BodyCheck.Core;

using GdUnit4;

using Godot;

using static GdUnit4.Assertions;

namespace BodyCheck.Game.Tests;

/// <summary>
/// <see cref="PhysicsLayer"/> names the same layers as the project's settings, which this project copies from the
/// game's. Each layer of one bit has a name in the settings, the same as its own, and each name in the settings has a
/// layer.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class PhysicsLayerTest
{
    private const int LayerCount = 32;

    private static string SettingsName(int layer) =>
        ProjectSettings.GetSetting($"layer_names/3d_physics/layer_{layer}").AsString();

    [TestCase]
    public void EachLayerIsNamedInTheSettings()
    {
        foreach (PhysicsLayer layer in Enum.GetValues<PhysicsLayer>().Where(layer => BitOperations.IsPow2((uint)layer)))
        {
            int number = BitOperations.TrailingZeroCount((uint)layer) + 1;
            AssertString(SettingsName(number)).IsEqual(layer.ToString());
        }
    }

    [TestCase]
    public void EachNameInTheSettingsIsALayer()
    {
        for (int number = 1; number <= LayerCount; number++)
        {
            string name = SettingsName(number);
            if (name.Length == 0)
            {
                continue;
            }
            AssertBool(Enum.TryParse(name, out PhysicsLayer layer)).IsTrue();
            AssertThat((uint)layer).IsEqual(1u << (number - 1));
        }
    }
}
