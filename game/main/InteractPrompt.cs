using BodyCheck.Core;
using BodyCheck.Core.Simulation;
using BodyCheck.Core.WorldData;

using Godot;

namespace BodyCheck;

/// <summary>
/// Shows that the player can interact: a speech bubble centred over the top of the trigger that the player would set
/// off by pressing up, while there is one. It belongs in the game's viewport, where it is drawn as part of the game's
/// image: over the world, under the colour pass and the menu. <c>Main</c> shows it while playing only. Its sizes are in
/// pixels at 320x240.
/// </summary>
public partial class InteractPrompt : Node2D
{
    private const string TexturePath = "res://assets/prompts/interact.png";

    // The gap between the top of the trigger and the bottom of the bubble.
    private const float Gap = 2;

    private readonly Texture2D _texture = GD.Load<Texture2D>(TexturePath);

    /// <summary>The world whose interact trigger is shown.</summary>
    public WorldNode? World { get; set; }

    public override void _Ready() => TextureFilter = TextureFilterEnum.Nearest;

    // The camera moves every frame, and the trigger can change on any tick.
    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        Camera3D? camera = GetViewport().GetCamera3D();
        if (camera is null || World?.InteractTrigger is not EntityConfig.Trigger trigger)
        {
            return;
        }

        var top = new Vector2(trigger.MapCenter.X, trigger.MapOrigin.Y + trigger.MapSize.Y);
        Vector2 bottomCentre = camera.UnprojectPosition(top.ToVector3()) - new Vector2(0, Gap);
        Vector2 size = _texture.GetSize();
        // Whole pixels, so that the bubble stays crisp as the camera moves.
        DrawTexture(_texture, (bottomCentre - new Vector2(size.X / 2, size.Y)).Round());
    }
}
