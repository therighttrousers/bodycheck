using Godot;

namespace BodyCheck.Look;

/// <summary>
/// Draws the game at a low resolution, scaled up to the window by the largest whole number that fits, with
/// nearest-neighbour sampling and black bars around it (decision record 003).
/// <para>
/// The game is drawn into <see cref="World"/>, a viewport of its own, so that what is drawn outside it, in
/// the window's own viewport, is at the window's full resolution. As a container of a viewport, it passes
/// the window's input on to it, with the mouse's position scaled to match.
/// </para>
/// </summary>
public partial class LowResView : SubViewportContainer
{
    private Vector2I? _resolution;

    public LowResView()
    {
        // Without stretch, the container draws the viewport at the viewport's own size, and Scale enlarges it.
        Stretch = false;
        TextureFilter = TextureFilterEnum.Nearest;
        AddChild(World);
    }

    /// <summary>The viewport the game is drawn in.</summary>
    public SubViewport World { get; } = new() { HandleInputLocally = false };

    /// <summary>The size the game is drawn at, or null to draw it at the window's own size.</summary>
    public Vector2I? Resolution
    {
        get => _resolution;
        set
        {
            _resolution = value;
            Fit();
        }
    }

    public override void _Ready()
    {
        RenderingServer.SetDefaultClearColor(Colors.Black); // The bars around the view.
        GetWindow().SizeChanged += Fit;
        Fit();
    }

    private void Fit()
    {
        if (!IsInsideTree())
        {
            return;
        }

        Vector2I window = GetWindow().Size;
        Vector2I size = _resolution ?? window;
        int scale = Mathf.Max(1, Mathf.Min(window.X / size.X, window.Y / size.Y));

        World.Size = size;
        Size = size;
        Scale = new Vector2(scale, scale);
        Position = (window - size * scale) / 2;
    }
}
