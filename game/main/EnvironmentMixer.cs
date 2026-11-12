using System;
using System.Collections.Generic;
using System.Linq;

using BodyCheck.Core.WorldData;

using Godot;

namespace BodyCheck;

/// <summary>
/// Draws a world's environments, each room's lighting and sky, blended by where the camera is, so that crossing from a
/// room to one of another environment changes the look smoothly, and going back undoes it exactly.
/// <list type="bullet">
/// <item>
/// Each room weighs from 1, deep inside it, to 0, at half of <see cref="BlendWidth"/> outside it, by the signed distance
/// of the view's centre from its box, so that two rooms that meet weigh a half each on the edge between them. An
/// environment weighs what its rooms do, and the weights are scaled to add up to 1. With no room near, the nearest one's
/// environment is the look.
/// </item>
/// <item>
/// Each environment's scene has a <c>WorldEnvironment</c>, whose <see cref="Godot.Environment"/> the mixer blends into
/// one of its own, and a <c>Lighting</c> node, whose lights it keeps, each with its energy scaled by the environment's
/// weight. A scene with no weight is hidden, so that its lights cost nothing.
/// </item>
/// <item>
/// The mixed environment's background is always a <see cref="ProceduralSkyMaterial"/>: an environment's background
/// must be one, or a colour, which is a sky of that colour throughout.
/// </item>
/// </list>
/// It processes while the tree is paused, as the camera does.
/// </summary>
public partial class EnvironmentMixer : Node3D
{
    /// <summary>How wide the band is across a room's edge in which its environment blends with the next, in tiles.</summary>
    public const float BlendWidth = 6;

    private readonly World _world;
    private readonly Camera3D _camera;
    private readonly RoomEnvironment[] _kinds;
    private readonly Node3D[] _scenes;
    private readonly (Light3D Light, float Energy)[][] _lights;
    private readonly PropertyMix _environmentMix;
    private readonly PropertyMix _skyMix;
    private readonly float[] _weights;

    /// <param name="world">The world whose rooms say which environment is where.</param>
    /// <param name="camera">The camera, whose position in the world's plane is the view's centre.</param>
    public EnvironmentMixer(World world, Camera3D camera)
    {
        _world = world;
        _camera = camera;
        _kinds = [.. world.Rooms.Select(room => room.Environment).Distinct()];
        _scenes = [.. _kinds.Select(AssetLibrary.GetEnvironment)];
        _weights = new float[_kinds.Length];

        var environments = new Godot.Environment[_kinds.Length];
        var skies = new ProceduralSkyMaterial[_kinds.Length];
        _lights = new (Light3D, float)[_kinds.Length][];
        for (int i = 0; i < _kinds.Length; i++)
        {
            Node3D scene = _scenes[i];
            var worldEnvironment = scene.GetNode<WorldEnvironment>("WorldEnvironment");
            environments[i] = worldEnvironment.Environment;
            skies[i] = SkyOf(_kinds[i], environments[i]);
            // Only the mixer's own is in the tree, since a viewport draws only one.
            scene.RemoveChild(worldEnvironment);
            worldEnvironment.Free();
            _lights[i] = [.. Lights(scene.GetNode("Lighting")).Select(light => (light, light.LightEnergy))];
            AddChild(scene);
        }

        var sky = (ProceduralSkyMaterial)skies[0].Duplicate();
        var mixed = (Godot.Environment)environments[0].Duplicate();
        mixed.BackgroundMode = Godot.Environment.BGMode.Sky;
        mixed.Sky = new Sky { SkyMaterial = sky };
        _environmentMix = new PropertyMix(mixed, environments, ["background_mode", "sky"]);
        _skyMix = new PropertyMix(sky, skies, []);
        AddChild(new WorldEnvironment { Environment = mixed });
    }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Mix();
    }

    public override void _Process(double delta) => Mix();

    /// <summary>Weighs the environments by where the camera is, and blends them, if their weights have changed.</summary>
    private void Mix()
    {
        Vector3 position = _camera.GlobalPosition;
        float[] weights = Weights(new Vector2(position.X, position.Y));
        if (weights.SequenceEqual(_weights))
        {
            return;
        }
        weights.CopyTo(_weights, 0);

        _environmentMix.Apply(_weights);
        _skyMix.Apply(_weights);
        for (int i = 0; i < _kinds.Length; i++)
        {
            _scenes[i].Visible = _weights[i] > 0;
            foreach ((Light3D light, float energy) in _lights[i])
            {
                light.LightEnergy = energy * _weights[i];
            }
        }
    }

    /// <summary>Each environment's weight, at a point in the world, as the class's summary describes. They add up to 1.</summary>
    private float[] Weights(Vector2 point)
    {
        var weights = new float[_kinds.Length];
        foreach ((Room room, float distance) in _world.RoomsNear(point, BlendWidth / 2))
        {
            weights[Array.IndexOf(_kinds, room.Environment)] += Mathf.Clamp(0.5f - distance / BlendWidth, 0, 1);
        }
        float total = weights.Sum();
        if (total <= 0)
        {
            Room nearest = _world.RoomsNear(point, float.PositiveInfinity).MinBy(near => near.Distance).Room;
            weights[Array.IndexOf(_kinds, nearest.Environment)] = 1;
            return weights;
        }
        for (int i = 0; i < weights.Length; i++)
        {
            weights[i] /= total;
        }
        return weights;
    }

    /// <summary>An environment's sky: its own, or, for a background of a colour, a sky of that colour throughout.</summary>
    /// <exception cref="InvalidOperationException">When its background is neither.</exception>
    private static ProceduralSkyMaterial SkyOf(RoomEnvironment kind, Godot.Environment environment)
    {
        switch (environment.BackgroundMode)
        {
            case Godot.Environment.BGMode.Sky when environment.Sky?.SkyMaterial is ProceduralSkyMaterial sky:
                return sky;
            case Godot.Environment.BGMode.Color:
                Color color = environment.BackgroundColor;
                return new ProceduralSkyMaterial
                {
                    SkyTopColor = color,
                    SkyHorizonColor = color,
                    GroundBottomColor = color,
                    GroundHorizonColor = color,
                    SkyEnergyMultiplier = environment.BackgroundEnergyMultiplier,
                    GroundEnergyMultiplier = environment.BackgroundEnergyMultiplier,
                };
            default:
                throw new InvalidOperationException(
                    $"The {kind} environment's background is neither a colour nor a {nameof(ProceduralSkyMaterial)}.");
        }
    }

    /// <summary>Every light in a tree of nodes.</summary>
    private static IEnumerable<Light3D> Lights(Node node)
    {
        if (node is Light3D light)
        {
            yield return light;
        }
        foreach (Light3D child in node.GetChildren().SelectMany(Lights))
        {
            yield return child;
        }
    }

    /// <summary>
    /// Blends objects of one class into another of it, property by property, for the properties in which they differ:
    /// numbers, colours and vectors by their weights, and everything else, such as switches, choices and resources, from
    /// the heaviest, so that it changes where the weights cross.
    /// </summary>
    private sealed class PropertyMix
    {
        private readonly GodotObject _target;
        private readonly GodotObject[] _sources;
        private readonly StringName[] _properties;

        /// <param name="excluded">Properties the target keeps as it is.</param>
        public PropertyMix(GodotObject target, IReadOnlyList<GodotObject> sources, IReadOnlyList<string> excluded)
        {
            _target = target;
            _sources = [.. sources];
            _properties =
            [
                .. target.GetPropertyList()
                    .Where(property => ((PropertyUsageFlags)property["usage"].AsInt64()).HasFlag(PropertyUsageFlags.Storage))
                    .Select(property => property["name"].AsString())
                    .Where(name => !name.StartsWith("resource_") && name != "script" && !excluded.Contains(name))
                    .Where(name => _sources.Skip(1).Any(source => !Same(source.Get(name), _sources[0].Get(name))))
                    .Select(name => new StringName(name))
            ];
        }

        /// <summary>Sets the target's properties from the sources, which <paramref name="weights"/>, adding up to 1, weigh.</summary>
        public void Apply(IReadOnlyList<float> weights)
        {
            int heaviest = Enumerable.Range(0, weights.Count).MaxBy(i => weights[i]);
            foreach (StringName property in _properties)
            {
                Variant[] values = [.. _sources.Select(source => source.Get(property))];
                _target.Set(property, values[heaviest].VariantType switch
                {
                    Variant.Type.Float => values.Select((value, i) => value.AsDouble() * weights[i]).Sum(),
                    Variant.Type.Color =>
                        values.Select((value, i) => value.AsColor() * weights[i]).Aggregate((a, b) => a + b),
                    Variant.Type.Vector2 =>
                        values.Select((value, i) => value.AsVector2() * weights[i]).Aggregate((a, b) => a + b),
                    Variant.Type.Vector3 =>
                        values.Select((value, i) => value.AsVector3() * weights[i]).Aggregate((a, b) => a + b),
                    Variant.Type.Vector4 =>
                        values.Select((value, i) => value.AsVector4() * weights[i]).Aggregate((a, b) => a + b),
                    _ => values[heaviest],
                });
            }
        }

        private static bool Same(Variant a, Variant b) => a.VariantType == b.VariantType && Equals(a.Obj, b.Obj);
    }
}
