using BodyCheck.Core.WorldData;

using Godot;

namespace BodyCheck;

/// <summary>
/// Draws a world's <see cref="CameraMap"/>, for debugging it and the painting it's made from: a quad over the whole
/// world, at the face between the tiles and the tiles behind them, so that it shows in open space and the tiles hide
/// it. Where the view's centre may be is a bright, partly transparent colour, and the camera boundary a dim one. The map
/// is made the first time it shows, so that a world that never shows it doesn't pay for it.
/// </summary>
public partial class CameraMapView : MeshInstance3D
{
    /// <summary>The depth of the face between the tiles and those behind them.</summary>
    private const float Depth = -0.5f;

    private static readonly Color ValidColor = new(0.2f, 1, 0.4f, 0.6f);
    private static readonly Color BoundaryColor = new(1, 0.2f, 0.2f, 0.25f);

    private readonly World _world;

    public CameraMapView(World world)
    {
        _world = world;
        CastShadow = ShadowCastingSetting.Off;
    }

    public override void _Ready() => MakeIfShown();

    public override void _Notification(int what)
    {
        if (what == NotificationVisibilityChanged)
        {
            MakeIfShown();
        }
    }

    private void MakeIfShown()
    {
        if (Mesh is not null || !Visible || !IsInsideTree())
        {
            return;
        }
        var map = new CameraMap(_world);
        bool[,] boundary = CameraMap.Boundary(_world);
        var image = Image.CreateEmpty(map.Width, map.Height, useMipmaps: false, Image.Format.Rgba8);
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                Color color = map.IsValid(x, y) ? ValidColor
                    : boundary[x / CameraMap.PixelsPerTile.X, y / CameraMap.PixelsPerTile.Y] ? BoundaryColor
                    : Colors.Transparent;
                // An image's rows go down from the top.
                image.SetPixel(x, map.Height - 1 - y, color);
            }
        }
        Vector2 size = _world.Size;
        Mesh = new QuadMesh
        {
            Size = size,
            CenterOffset = new Vector3(size.X / 2, size.Y / 2, Depth),
            Material = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
                AlbedoTexture = ImageTexture.CreateFromImage(image),
            },
        };
    }
}
