using System.Collections.Generic;

using Godot;

namespace BodyCheck.Look;

/// <summary>
/// Puts vertices on a grid on the screen, as hardware of the mid-1990s did by having no position finer than a pixel.
/// The engine has no place for a vertex shader that applies to every material, and doesn't give out the shader it
/// generates for a <see cref="BaseMaterial3D"/>, which is what the game's materials are. So this replaces each one on
/// each node that enters the tree with a <see cref="ShaderMaterial"/> of the same look, leaving meshes and models
/// alone. A material with features the shader lacks stays as it is, and its vertices aren't snapped. A change to a
/// material after it's replaced doesn't show.
/// </summary>
public partial class VertexSnap : Node
{
    private const string EnabledParameter = "vertex_snap_enabled";

    // MODES, FILTER and EXTRAS are filled in for each kind of material. Writing POSITION anywhere replaces it everywhere,
    // so it's written whether snapping is on or not. Directional shadows have an orthographic projection, the only kind
    // that isn't snapped: a shadow pass has its own grid, and shadows that move in steps would slide over surfaces that
    // don't.
    private const string ShaderTemplate = $$"""
        shader_type spatial;
        render_mode MODES;

        global uniform bool {{EnabledParameter}};

        uniform vec4 albedo : source_color = vec4(1.0);
        uniform sampler2D albedo_texture : source_color, hint_default_white, FILTER, repeat_enable;
        uniform bool albedo_from_vertex_color;
        uniform float metallic;
        uniform sampler2D metallic_texture : hint_default_white, FILTER, repeat_enable;
        uniform vec4 metallic_channel;
        uniform float roughness;
        uniform sampler2D roughness_texture : hint_default_white, FILTER, repeat_enable;
        uniform vec4 roughness_channel;
        uniform float specular;
        uniform sampler2D normal_texture : hint_roughness_normal, FILTER, repeat_enable;
        uniform float normal_scale;
        uniform vec3 emission : source_color;
        uniform float emission_energy;
        uniform sampler2D emission_texture : source_color, hint_default_black, FILTER, repeat_enable;
        uniform vec3 uv1_scale;
        uniform vec3 uv1_offset;

        void vertex() {
            UV = UV * uv1_scale.xy + uv1_offset.xy;

            vec4 clip = PROJECTION_MATRIX * (MODELVIEW_MATRIX * vec4(VERTEX, 1.0));
            if ({{EnabledParameter}} && PROJECTION_MATRIX[3][3] == 0.0 && clip.w > 0.0) {
                vec2 grid = VIEWPORT_SIZE * 0.5;
                clip.xy = round(clip.xy / clip.w * grid) / grid * clip.w;
            }
            POSITION = clip;
        }

        void fragment() {
            vec4 color = albedo * texture(albedo_texture, UV);
            if (albedo_from_vertex_color) {
                color *= COLOR;
            }
            ALBEDO = color.rgb;
            METALLIC = metallic * dot(texture(metallic_texture, UV), metallic_channel);
            ROUGHNESS = roughness * dot(texture(roughness_texture, UV), roughness_channel);
            SPECULAR = specular;
            EMISSION = (emission + texture(emission_texture, UV).rgb) * emission_energy;
            EXTRAS
        }
        """;

    private readonly Dictionary<string, Shader> _shaders = [];
    private readonly Dictionary<Material, Material> _replacements = [];
    private readonly Dictionary<Mesh, Mesh> _meshes = [];
    // Nodes that entered the tree since the last frame.
    private readonly List<GeometryInstance3D> _added = [];

    private static bool s_parameterAdded;

    public VertexSnap()
    {
        if (!s_parameterAdded)
        {
            RenderingServer.GlobalShaderParameterAdd(EnabledParameter, RenderingServer.GlobalShaderParameterType.Bool, false);
            s_parameterAdded = true;
        }
    }

    /// <summary>Whether vertices are snapped to whole pixels.</summary>
    public bool Enabled
    {
        set => RenderingServer.GlobalShaderParameterSet(EnabledParameter, value);
    }

    public override void _EnterTree() => GetTree().NodeAdded += OnNodeAdded;

    public override void _ExitTree() => GetTree().NodeAdded -= OnNodeAdded;

    public override void _Ready() => ProcessMode = ProcessModeEnum.Always;

    // On the next frame, since a node may change its materials as it starts.
    public override void _Process(double delta)
    {
        foreach (GeometryInstance3D node in _added)
        {
            if (IsInstanceValid(node))
            {
                Replace(node);
            }
        }
        _added.Clear();
    }

    private void OnNodeAdded(Node node)
    {
        if (node is GeometryInstance3D geometry)
        {
            _added.Add(geometry);
        }
    }

    private void Replace(GeometryInstance3D node)
    {
        if (node.MaterialOverride is not null)
        {
            node.MaterialOverride = Replacement(node.MaterialOverride);
        }
        else if (node is MeshInstance3D { Mesh: not null } instance)
        {
            for (int i = 0; i < instance.Mesh.GetSurfaceCount(); i++)
            {
                if (instance.GetActiveMaterial(i) is Material material)
                {
                    instance.SetSurfaceOverrideMaterial(i, Replacement(material));
                }
            }
        }
        else if (node is MultiMeshInstance3D { Multimesh.Mesh: not null } multi)
        {
            multi.Multimesh.Mesh = Replacement(multi.Multimesh.Mesh);
        }
    }

    /// <summary>A copy of <paramref name="mesh"/> with its materials replaced, so that the mesh itself is unchanged.</summary>
    private Mesh Replacement(Mesh mesh)
    {
        if (!_meshes.TryGetValue(mesh, out Mesh? copy))
        {
            copy = (Mesh)mesh.Duplicate();
            for (int i = 0; i < copy.GetSurfaceCount(); i++)
            {
                if (copy.SurfaceGetMaterial(i) is Material material)
                {
                    copy.SurfaceSetMaterial(i, Replacement(material));
                }
            }
            _meshes[mesh] = copy;
            _meshes[copy] = copy;
        }
        return copy;
    }

    private Material Replacement(Material material)
    {
        if (!_replacements.TryGetValue(material, out Material? replacement))
        {
            replacement = material is BaseMaterial3D standard ? Replacement(standard) : material;
            _replacements[material] = replacement;
            _replacements[replacement] = replacement;
        }
        return replacement;
    }

    private Material Replacement(BaseMaterial3D material)
    {
        if (Unsupported(material) is string feature)
        {
            GD.Print($"Vertex snap: skipping material \"{material.ResourceName}\", for its {feature}.");
            return material;
        }

        var replacement = new ShaderMaterial { Shader = ShaderFor(material) };
        replacement.SetShaderParameter("albedo", material.AlbedoColor);
        replacement.SetShaderParameter("albedo_texture", material.AlbedoTexture);
        replacement.SetShaderParameter("albedo_from_vertex_color", material.VertexColorUseAsAlbedo);
        replacement.SetShaderParameter("metallic", material.Metallic);
        replacement.SetShaderParameter("metallic_texture", material.MetallicTexture);
        replacement.SetShaderParameter("metallic_channel", Channel(material.MetallicTextureChannel));
        replacement.SetShaderParameter("roughness", material.Roughness);
        replacement.SetShaderParameter("roughness_texture", material.RoughnessTexture);
        replacement.SetShaderParameter("roughness_channel", Channel(material.RoughnessTextureChannel));
        replacement.SetShaderParameter("specular", material.MetallicSpecular);
        replacement.SetShaderParameter("normal_texture", material.NormalTexture);
        replacement.SetShaderParameter("normal_scale", material.NormalScale);
        replacement.SetShaderParameter("emission", material.Emission);
        replacement.SetShaderParameter("emission_energy", material.EmissionEnabled ? material.EmissionEnergyMultiplier : 0);
        replacement.SetShaderParameter("emission_texture", material.EmissionTexture);
        replacement.SetShaderParameter("uv1_scale", material.Uv1Scale);
        replacement.SetShaderParameter("uv1_offset", material.Uv1Offset);
        return replacement;
    }

    /// <summary>The weights that pick <paramref name="channel"/> out of a colour.</summary>
    private static Vector4 Channel(BaseMaterial3D.TextureChannel channel) => channel switch
    {
        BaseMaterial3D.TextureChannel.Red => new Vector4(1, 0, 0, 0),
        BaseMaterial3D.TextureChannel.Green => new Vector4(0, 1, 0, 0),
        BaseMaterial3D.TextureChannel.Blue => new Vector4(0, 0, 1, 0),
        BaseMaterial3D.TextureChannel.Alpha => new Vector4(0, 0, 0, 1),
        _ => new Vector4(1, 1, 1, 0) / 3,
    };

    private static string? Unsupported(BaseMaterial3D material)
    {
        if (material.Transparency is not (BaseMaterial3D.TransparencyEnum.Disabled
            or BaseMaterial3D.TransparencyEnum.Alpha
            or BaseMaterial3D.TransparencyEnum.AlphaDepthPrePass))
        {
            return material.Transparency.ToString();
        }
        return (material.RefractionEnabled, material.AOEnabled, material.RimEnabled, material.ClearcoatEnabled) switch
        {
            (true, _, _, _) => "refraction",
            (_, true, _, _) => "ambient occlusion texture",
            (_, _, true, _) => "rim",
            (_, _, _, true) => "clearcoat",
            _ when material.Uv1Triplanar => "triplanar mapping",
            _ when material.BillboardMode != BaseMaterial3D.BillboardModeEnum.Disabled => "billboard",
            _ when material.EmissionOperator != BaseMaterial3D.EmissionOperatorEnum.Add => "emission operator",
            _ => null,
        };
    }

    private Shader ShaderFor(BaseMaterial3D material)
    {
        string extras = material.Transparency == BaseMaterial3D.TransparencyEnum.Disabled ? "" : "ALPHA = color.a;\n";
        if (material.NormalEnabled)
        {
            extras += "NORMAL_MAP = texture(normal_texture, UV).rgb;\nNORMAL_MAP_DEPTH = normal_scale;\n";
        }
        string cull = material.CullMode switch
        {
            BaseMaterial3D.CullModeEnum.Front => "cull_front",
            BaseMaterial3D.CullModeEnum.Disabled => "cull_disabled",
            _ => "cull_back",
        };
        string diffuse = material.DiffuseMode switch
        {
            BaseMaterial3D.DiffuseModeEnum.Lambert => "diffuse_lambert",
            BaseMaterial3D.DiffuseModeEnum.LambertWrap => "diffuse_lambert_wrap",
            BaseMaterial3D.DiffuseModeEnum.Toon => "diffuse_toon",
            _ => "diffuse_burley",
        };
        string specular = material.SpecularMode switch
        {
            BaseMaterial3D.SpecularModeEnum.Toon => "specular_toon",
            BaseMaterial3D.SpecularModeEnum.Disabled => "specular_disabled",
            _ => "specular_schlick_ggx",
        };
        string modes = $"blend_mix, depth_draw_opaque, {cull}, {diffuse}, {specular}";
        if (material.ShadingMode == BaseMaterial3D.ShadingModeEnum.Unshaded)
        {
            modes += ", unshaded";
        }
        if (material.Transparency == BaseMaterial3D.TransparencyEnum.AlphaDepthPrePass)
        {
            modes += ", depth_prepass_alpha";
        }
        string filter = material.TextureFilter switch
        {
            BaseMaterial3D.TextureFilterEnum.Nearest => "filter_nearest",
            BaseMaterial3D.TextureFilterEnum.Linear => "filter_linear",
            BaseMaterial3D.TextureFilterEnum.NearestWithMipmaps => "filter_nearest_mipmap",
            _ => "filter_linear_mipmap",
        };

        string key = $"{modes} {filter} {extras}";
        if (!_shaders.TryGetValue(key, out Shader? shader))
        {
            shader = new Shader
            {
                Code = ShaderTemplate.Replace("MODES", modes).Replace("FILTER", filter).Replace("EXTRAS", extras),
            };
            _shaders[key] = shader;
        }
        return shader;
    }
}
