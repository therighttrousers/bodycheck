using DialogueManagerRuntime;

using Godot;

namespace BodyCheck;

/// <summary>
/// Dialogue Manager's <see cref="DialogueLabel"/>, which types a line out, pausing at punctuation, and handles its
/// inline tags and runs its inline mutations as it reaches them, but which shows the line garbled by
/// <see cref="Garble"/>, shifting which glyphs are garbled as it is shown.
/// <para>
/// The label's settings are in its scene, not its class, so they are set here: without them it is a plain
/// <c>RichTextLabel</c>, which, without <c>FitContent</c>, asks for no height in a container and shows nothing.
/// </para>
/// </summary>
public partial class GarbledDialogueLabel : DialogueLabel
{
    public GarbledDialogueLabel()
    {
        BbcodeEnabled = true;
        FitContent = true;
        ScrollActive = false;
        ShortcutKeysEnabled = false;
        // Shows text by its visible characters, which inline mutations are placed by, counted after shaping.
        VisibleCharactersBehavior = TextServer.VisibleCharactersBehavior.CharsAfterShaping;
        AutowrapMode = TextServer.AutowrapMode.WordSmart;
    }

    /// <summary>
    /// How long the garble stays still before it shifts to other glyphs, in seconds, on the same clock as the typing.
    /// </summary>
    private const double ShiftSeconds = 0.5;

    // How long the line has been shown, and the garble's step, which counts its shifts from when it was.
    private double _shownSeconds;
    private int _step;

    /// <summary>The fraction of the line's letters and digits garbled, from 0 to 1.</summary>
    public float GarbleFraction { get; set; }

    /// <summary>Types the line out, and shifts its garble every <see cref="ShiftSeconds"/>.</summary>
    public override void _Process(double delta)
    {
        base._Process(delta);
        if (GarbleFraction == 0 || DialogueLine is null)
        {
            return;
        }
        _shownSeconds += delta;
        int step = (int)(_shownSeconds / ShiftSeconds);
        if (step != _step)
        {
            _step = step;
            // How much of the line is shown is put back after the text is set. The label's indices, by which it
            // pauses and runs inline mutations, still hold, since the garble replaces glyphs one for one.
            int visible = VisibleCharacters;
            SetGarbledText();
            VisibleCharacters = visible;
        }
    }

    /// <summary>Sets the text from the line, garbled, with the garble back at its first step.</summary>
    protected override void UpdateText()
    {
        _shownSeconds = 0;
        _step = 0;
        SetGarbledText();
    }

    /// <summary>
    /// Sets the text from the line, garbled at the current step from a seed taken from its ID: its static ID, which is
    /// its translation key, or Dialogue Manager's own ID if it has none.
    /// </summary>
    private void SetGarbledText()
    {
        DialogueLine? line = DialogueLine;
        if (line is null || !IsInstanceValid(line))
        {
            Text = "";
            return;
        }
        string seed = line.StaticId != "" ? line.StaticId : line.Id;
        Text = Garble.Apply(line.Text, GarbleFraction, seed, _step);
    }
}
