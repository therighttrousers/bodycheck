using BodyCheck.Core;

using Godot;

namespace BodyCheck;

/// <summary>
/// Shows how the player's camera is framed, for debugging and tuning the framing and the painting of the camera
/// boundary. F1 switches it on and off.
/// <list type="bullet">
/// <item>
/// The <see cref="CameraMap"/>, on a quad over the whole world at the face between the tiles and the tiles behind them,
/// so that it shows in open space and the tiles hide it: a bright, partly transparent colour where the view's centre may
/// be, and a dim one on the boundary. Its texture is made the first time it shows.
/// </item>
/// <item>
/// The wanted framing, as a cross, and the view's centre as framed, as a diamond, with a line from one to the other,
/// drawn through the tiles. They are read from <see cref="Telemetry"/> each frame while showing, so that what shows is
/// what the camera reported.
/// </item>
/// </list>
/// It processes while the tree is paused, as the camera does, and doesn't process while not showing.
/// </summary>
public partial class CameraDebug : Node3D
{
    /// <summary>The depth of the face between the tiles and those behind them, where the map is drawn.</summary>
    private const float MapDepth = -0.5f;
    /// <summary>The depth of the tiles' front faces, at which the camera's framing is measured.</summary>
    private const float PointDepth = 0.5f;
    /// <summary>Half the size of a point's mark, in tiles.</summary>
    private const float MarkSize = 0.3f;
    /// <summary>Half the width of the marks' lines, in tiles.</summary>
    private const float LineHalfWidth = 0.5f / Constants.TilePixels;

    private static readonly Color ValidColor = new(0.2f, 1, 0.4f, 0.1f);
    private static readonly Color BoundaryColor = new(1, 0.2f, 0.2f, 0.1f);
    private static readonly Color WantedColor = new(1, 0.9f, 0.2f, 0.5f);
    private static readonly Color TargetColor = new(1, 1, 1, 0.5f);

    private readonly CameraMap _map;
    private readonly MeshInstance3D _mapQuad = new() { CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
    // Redrawn each frame, from the telemetry.
    private readonly ImmediateMesh _pointsMesh = new();

    public CameraDebug(CameraMap map)
    {
        _map = map;
        AddChild(_mapQuad);
        AddChild(new MeshInstance3D
        {
            Mesh = _pointsMesh,
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                NoDepthTest = true,
                // So that it needn't matter which way round a triangle's corners go.
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                VertexColorUseAsAlbedo = true,
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off,
        });
    }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Shown();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationVisibilityChanged && IsInsideTree())
        {
            Shown();
        }
    }

    public override void _Process(double delta)
    {
        _pointsMesh.ClearSurfaces();
        if (Telemetry.Get(Metric.CameraWanted) is not Vector2 wanted
            || Telemetry.Get(Metric.CameraTarget) is not Vector2 target)
        {
            return;
        }
        _pointsMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        Line(wanted, target, WantedColor);
        Line(wanted + new Vector2(-MarkSize, -MarkSize), wanted + new Vector2(MarkSize, MarkSize), WantedColor);
        Line(wanted + new Vector2(-MarkSize, MarkSize), wanted + new Vector2(MarkSize, -MarkSize), WantedColor);
        Vector2[] corners = [new(MarkSize, 0), new(0, MarkSize), new(-MarkSize, 0), new(0, -MarkSize)];
        for (int i = 0; i < corners.Length; i++)
        {
            Line(target + corners[i], target + corners[(i + 1) % corners.Length], TargetColor);
        }
        _pointsMesh.SurfaceEnd();
    }

    /// <summary>Processes only while showing, and makes the map's texture the first time it shows.</summary>
    private void Shown()
    {
        SetProcess(Visible);
        if (!Visible)
        {
            _pointsMesh.ClearSurfaces();
            return;
        }
        if (_mapQuad.Mesh is not null)
        {
            return;
        }
        var image = Image.CreateEmpty(_map.Width, _map.Height, useMipmaps: false, Image.Format.Rgba8);
        for (int y = 0; y < _map.Height; y++)
        {
            for (int x = 0; x < _map.Width; x++)
            {
                Color color = _map.IsValid(x, y) ? ValidColor
                    : _map.IsBoundary(x / CameraMap.PixelsPerTile.X, y / CameraMap.PixelsPerTile.Y) ? BoundaryColor
                    : Colors.Transparent;
                // An image's rows go down from the top.
                image.SetPixel(x, _map.Height - 1 - y, color);
            }
        }
        var size = new Vector2((float)_map.Width / CameraMap.PixelsPerTile.X, (float)_map.Height / CameraMap.PixelsPerTile.Y);
        _mapQuad.Mesh = new QuadMesh
        {
            Size = size,
            CenterOffset = new Vector3(size.X / 2, size.Y / 2, MapDepth),
            Material = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
                AlbedoTexture = ImageTexture.CreateFromImage(image),
            },
        };
    }

    /// <summary>Adds a line from <paramref name="from"/> to <paramref name="to"/>, as a thin quad.</summary>
    private void Line(Vector2 from, Vector2 to, Color color)
    {
        Vector2 along = to - from;
        if (along.LengthSquared() < 1e-8f)
        {
            return;
        }
        Vector2 side = along.Orthogonal().Normalized() * LineHalfWidth;
        Vector3 a = At(from - side), b = At(from + side), c = At(to + side), d = At(to - side);
        foreach (Vector3 corner in (Vector3[])[a, b, c, a, c, d])
        {
            _pointsMesh.SurfaceSetColor(color);
            _pointsMesh.SurfaceAddVertex(corner);
        }
    }

    private static Vector3 At(Vector2 point) => new(point.X, point.Y, PointDepth);
}
