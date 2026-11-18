using System;

using Godot;

namespace BodyCheck.Environments;

/// <summary>
/// A layer of an environment's backdrop, under the scene's <c>Backdrop</c> node: a quad at some depth behind the tiles,
/// facing the camera, showing one copy of a picture, which may repeat across, or up and down, without end.
/// <list type="bullet">
/// <item>
/// It is authored as an ordinary quad, with its mesh's own UVs: where it is, and its size, scaled, are where one copy of
/// the picture is. The script doesn't run in the editor, so there it stays so.
/// </item>
/// <item>
/// In the game, along each axis on which the picture repeats, the quad is stretched to cover the view at its depth and
/// moves with the camera, while its UVs are set so that the picture stays where it was authored, repeated. Along an axis
/// on which it doesn't, the quad keeps its authored place and size. The picture stays fixed in the world either way, so
/// the camera's perspective gives the parallax: the deeper the layer, the slower it moves.
/// </item>
/// </list>
/// Its mesh must be a <see cref="QuadMesh"/> and its material a <see cref="StandardMaterial3D"/>, and it may be scaled
/// but not turned. It takes copies of both, so that layers that share them don't change each other.
/// See <c>docs/designs/rendering.md</c>.
/// </summary>
public partial class BackdropLayer : MeshInstance3D
{
    // How much more than the view a repeating quad covers on each side, as a fraction of the view, so that its edges
    // never show while the camera moves on before the quad catches up.
    private const float Margin = 0.1f;

    private QuadMesh _quad = null!;
    private StandardMaterial3D _material = null!;
    // Where the authored quad's centre is, and its size, in the world: where one copy of the picture is.
    private Vector3 _centre;
    private Vector2 _size;
    // The picture's size in texels, or zero if it has no texture.
    private Vector2 _texels;

    /// <summary>Whether the picture repeats across.</summary>
    [Export] public bool RepeatX { get; set; } = true;

    /// <summary>Whether the picture repeats up and down. Turn it off for a horizon, such as mountains.</summary>
    [Export] public bool RepeatY { get; set; } = true;

    public override void _Ready()
    {
        Basis basis = GlobalBasis;
        Vector3 scale = basis.Scale;
        if (!basis.IsEqualApprox(Basis.FromScale(scale)))
        {
            throw new InvalidOperationException($"The backdrop layer {GetPath()} is turned; it may only be scaled.");
        }
        if (Mesh is not QuadMesh quad)
        {
            throw new InvalidOperationException($"The backdrop layer {GetPath()}'s mesh isn't a {nameof(QuadMesh)}.");
        }
        if (GetActiveMaterial(0) is not StandardMaterial3D material)
        {
            throw new InvalidOperationException(
                $"The backdrop layer {GetPath()}'s material isn't a {nameof(StandardMaterial3D)}.");
        }

        _centre = GlobalPosition;
        _size = new Vector2(quad.Size.X * scale.X, quad.Size.Y * scale.Y);
        _texels = material.AlbedoTexture?.GetSize() ?? Vector2.Zero;

        // From here on, the quad is sized in the world's units.
        GlobalBasis = Basis.Identity;
        _quad = (QuadMesh)quad.Duplicate();
        Mesh = _quad;
        _material = (StandardMaterial3D)material.Duplicate();
        _material.Uv1Triplanar = false;
        _material.Uv1WorldTriplanar = false;
        _material.TextureRepeat = true;
        MaterialOverride = _material;
    }

    public override void _Process(double delta)
    {
        Camera3D? camera = GetViewport().GetCamera3D();
        if (camera is null)
        {
            return;
        }

        // The view's half size at the layer's depth. The camera keeps its height, so its field of view is vertical.
        Vector3 eye = camera.GlobalPosition;
        float depth = eye.Z - _centre.Z;
        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        float halfHeight = depth * Mathf.Tan(Mathf.DegToRad(camera.Fov / 2)) * (1 + Margin);
        float halfWidth = halfHeight * viewport.X / viewport.Y;

        (float x, float width, float u, float uScale) =
            Axis(RepeatX, eye.X, halfWidth, _centre.X, _size.X, _texels.X, sign: 1);
        (float y, float height, float v, float vScale) =
            Axis(RepeatY, eye.Y, halfHeight, _centre.Y, _size.Y, _texels.Y, sign: -1);
        GlobalPosition = new Vector3(x, y, _centre.Z);
        _quad.Size = new Vector2(width, height);
        _material.Uv1Scale = new Vector3(uScale, vScale, 1);
        _material.Uv1Offset = new Vector3(u, v, 0);

        // With a perspective camera, Godot sorts transparent things by their straight-line distance from it, so a near
        // layer far above or below the view could sort behind a deeper one. Offsetting the distance down to the depth
        // along the view, which is along -Z since the camera never turns, sorts the layers by depth.
        Vector3 centre = GlobalTransform * GetAabb().GetCenter();
        SortingOffset = eye.DistanceTo(centre) - (eye.Z - centre.Z);
    }

    /// <summary>
    /// The quad's centre and size along one axis, and the scale and offset of its UVs along it. A picture that repeats
    /// covers the view, centred on the camera, with UVs that put a copy where the authored quad was. One that doesn't is
    /// where it was authored, with UVs from half a texel in from one edge of the picture to half a texel in from the
    /// other: the texture repeats, so a pixel on the quad's edge whose UV lands a hair outside 0 to 1 would otherwise
    /// show the picture's opposite edge, such as a line along the top of mountains whose bottom row is opaque.
    /// </summary>
    /// <param name="texels">The picture's size along the axis in texels, or 0 if it has no texture.</param>
    /// <param name="sign">1 if the UV grows with the axis, as U does with x; -1 if it shrinks, as V does with y.</param>
    private static (float Centre, float Size, float Offset, float Scale) Axis(
        bool repeat, float eye, float halfView, float centre, float size, float texels, float sign)
    {
        if (!repeat)
        {
            float inset = texels > 0 ? 0.5f / texels : 0;
            return (centre, size, inset, 1 - 2 * inset);
        }
        // The UV at the quad's starting edge, the left or the top, measured in copies of the picture from the starting
        // edge of the authored one. Only its fraction matters, since the texture repeats, and it keeps precision.
        float start = eye - sign * halfView;
        float offset = sign * (start - centre) / size + 0.5f;
        return (eye, 2 * halfView, offset - Mathf.Floor(offset), 2 * halfView / size);
    }
}
