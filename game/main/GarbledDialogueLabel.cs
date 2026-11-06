using DialogueManagerRuntime;

using Godot;

namespace BodyCheck;

/// <summary>
/// Dialogue Manager's <see cref="DialogueLabel"/>, which types a line out, pausing at punctuation, and handles its
/// inline tags and runs its inline mutations as it reaches them, but which shows the line garbled by
/// <see cref="Garble"/>.
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

    /// <summary>The fraction of the line's letters and digits garbled, from 0 to 1.</summary>
    public float GarbleFraction { get; set; }

    /// <summary>
    /// Sets the text from the line, garbled from a seed taken from its ID: its static ID, which is its translation key,
    /// or Dialogue Manager's own ID if it has none.
    /// </summary>
    protected override void UpdateText()
    {
        DialogueLine? line = DialogueLine;
        if (line is null || !IsInstanceValid(line))
        {
            Text = "";
            return;
        }
        string seed = line.StaticId != "" ? line.StaticId : line.Id;
        Text = Garble.Apply(line.Text, GarbleFraction, seed);
    }
}
