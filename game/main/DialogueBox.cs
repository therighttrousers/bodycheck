using System;
using System.Linq;
using System.Threading.Tasks;

using BodyCheck.Core;
using BodyCheck.Core.Scripts;
using BodyCheck.Core.Tasks;

using DialogueManagerRuntime;

using Godot;

namespace BodyCheck;

/// <summary>
/// The game's <see cref="IDialoguePresenter"/>, and the text box that shows the dialogue, from Dialogue Manager's files
/// in <see cref="DialogueDirectory"/>, in the font and size of <see cref="ThemePath"/>. It belongs in the game's
/// viewport, on a layer above the colour pass, along its bottom edge. Beside the line is the speaker's avatar, if they
/// have one in <see cref="AvatarDirectory"/>. The box itself is still a placeholder. <see cref="Main"/> shows it only in a
/// cutscene, and within that, it shows only while a dialogue is presented, so that a cutscene with no dialogue
/// has no box.
/// <para>
/// <see cref="Present"/> starts the dialogue at once if none is presented, and otherwise queues it, to start when the
/// ones before it have ended. It holds no cutscene: what presents a dialogue holds one, as Core's dialogue runner does,
/// so that the game's modes know nothing of dialogue. Each line types out, and its responses show once it
/// has. Jump skips the typing, or once a line has typed out, goes on to the next, and up and down choose a response.
/// </para>
/// <para>
/// A dialogue is presented by an async method on the tick it is presented on, the world's, which waits for each line
/// and for the player to go on from it: it ends when the dialogue does, so what waits for it resumes in that tick. When
/// the world is dropped, stopping its tick, the method unwinds like any other, and the box drops the dialogue. Dialogue
/// Manager fetches each line on Godot's frames, with a .NET <c>Task</c>, a frame or more after it is asked, and the
/// player's input comes off the tick too: the method checks in each tick whether they have come.
/// </para>
/// </summary>
public partial class DialogueBox : CanvasLayer, IDialoguePresenter
{
    private const string DialogueDirectory = "res://dialogue";
    private const string DialogueExtension = ".dialogue";
    /// <summary>
    /// The theme, whose default font and size the box's text is in. The font falls back to the tofu font for
    /// <see cref="Garble.Tofu"/>. A locale with a font of its own would remap the theme.
    /// </summary>
    private const string ThemePath = "res://assets/fonts/dialogue_theme.tres";
    /// <summary>
    /// Where the speakers' avatars are: each a PNG, 48 by 48 pixels, named for the speaker as the dialogue files name
    /// them, before translation, as <c>Voice.png</c>.
    /// </summary>
    private const string AvatarDirectory = "res://assets/avatars";

    // So that one dialogue is presented at a time, and the next starts when the one before has ended.
    private readonly ReentrantTask _presenting = new(Reentry.Queue);
    // The line being shown, or null while the next is being fetched.
    private DialogueLine? _line;
    private int _response;
    // What the player has gone on to from the line shown: the next line's ID, or null until they go on.
    private string? _next;
    // Whether the line shown has typed out. Not the label's IsTyping, which is false while an inline mutation, such as
    // a wait, is awaited partway through the line: going on then would end the line while the label still types it.
    private bool _typedOut;

    private readonly TextureRect _avatar = new();
    // The avatar's border and background, shown only while the speaker has an avatar.
    private readonly PanelContainer _avatarBackground = new()
    {
        CustomMinimumSize = new Vector2I(50, 50),
        CustomMaximumSize = new Vector2I(50, 50),
        SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
    };
    private readonly Label _character = new();
    private readonly GarbledDialogueLabel _text = new();
    private readonly VBoxContainer _responses = new();
    // Everything the box shows, which it shows only while a dialogue is presented.
    private readonly MarginContainer _shown = new() { Visible = false };

    public DialogueBox()
    {
        Layer = 2; // Above the colour pass, so the dither leaves it alone.
        Visible = false;

        _shown.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        _shown.GrowVertical = Control.GrowDirection.Begin;
        // Every line's height comes from the theme's font, so that a locale's font sets it.
        _shown.Theme = GD.Load<Theme>(ThemePath);
        foreach (string side in new[] { "left", "right", "bottom" })
        {
            _shown.AddThemeConstantOverride($"margin_{side}", 4);
        }
        AddChild(_shown);

        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.1f, 0.1f, 0.15f, 0.9f),
            ContentMarginLeft = 6,
            ContentMarginRight = 6,
            ContentMarginTop = 2,
            ContentMarginBottom = 4,
            AntiAliasing = false,
        });
        _shown.AddChild(panel);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        panel.AddChild(row);

        _avatarBackground.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0, 0, 0, 0.5f),
            BorderColor = new Color(1, 1, 1, 0.5f),
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
        });
        _avatarBackground.AddChild(_avatar);
        row.AddChild(_avatarBackground);

        var lines = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        lines.AddThemeConstantOverride("separation", 0);
        row.AddChild(lines);

        _character.Modulate = new Color(1, 0.85f, 0.4f);
        lines.AddChild(_character);
        lines.AddChild(_text);
        lines.AddChild(_responses);
        _text.FinishedTyping += () =>
        {
            _typedOut = true;
            ShowResponses();
        };
    }

    /// <summary>Whether a dialogue has started and not yet ended.</summary>
    public bool IsPresenting { get; private set; }

    /// <inheritdoc/>
    public TickTask Present(ITick tick, string file, string title) =>
        _presenting.Run(() => Run(tick, file, title));

    /// <summary>
    /// Presents the dialogue, line by line, until it ends. A missing file, a garble tag that isn't valid, or an error in
    /// Dialogue Manager fails it. However it ends, even by its tick being stopped, the box drops it.
    /// </summary>
    private async TickTask Run(ITick tick, string file, string title)
    {
        Resource resource = Load(file)
            ?? throw new System.IO.FileNotFoundException($"There is no dialogue file {PathOf(file)}.");
        IsPresenting = true;
        UpdateShown();
        try
        {
            string key = title;
            while (true)
            {
                _line = null;
                Task<DialogueLine?> fetching = DialogueManager.GetNextDialogueLine(resource, key);
                await tick.UntilCompleted(fetching);
                if (fetching.Result is not DialogueLine line)
                {
                    return;
                }
                try
                {
                    _text.GarbleFraction = Garble.FractionOf(line.Tags);
                }
                catch (FormatException e)
                {
                    throw new FormatException($"In the dialogue {file}, title {title}: {e.Message}", e);
                }
                _line = line;
                _response = 0;
                _next = null;
                ShowLine();
                await tick.While(() => _next is null);
                key = _next!;
            }
        }
        finally
        {
            IsPresenting = false;
            _line = null;
            _next = null;
            _text.DialogueLine = null;
            ClearResponses();
            UpdateShown();
        }
    }

    /// <inheritdoc/>
    public bool Holds(string file, string title) =>
        Load(file) is Resource resource && resource.Call("get_cues").AsStringArray().Contains(title);

    private static string PathOf(string file) => $"{DialogueDirectory}/{file}{DialogueExtension}";

    /// <summary>The dialogue file <paramref name="file"/> names, or null if there is none.</summary>
    private static Resource? Load(string file)
    {
        string path = PathOf(file);
        return ResourceLoader.Exists(path) ? GD.Load<Resource>(path) : null;
    }

    /// <summary>Shows the line, and starts typing it out. Its responses show once it has.</summary>
    private void ShowLine()
    {
        _character.Text = _line!.Character;
        _character.Visible = _line.Character != "";
        string avatar = $"{AvatarDirectory}/{_line.Character}.png";
        _avatar.Texture = _line.Character != "" && ResourceLoader.Exists(avatar) ? GD.Load<Texture2D>(avatar) : null;
        _avatarBackground.Visible = _avatar.Texture is not null;
        _text.DialogueLine = _line;
        ClearResponses();
        _typedOut = false;
        _text.TypeOut();
    }

    private void ClearResponses()
    {
        foreach (Node child in _responses.GetChildren())
        {
            _responses.RemoveChild(child);
            child.QueueFree();
        }
    }

    /// <summary>Shows the line's responses, with the chosen one marked.</summary>
    private void ShowResponses()
    {
        ClearResponses();
        if (_line is null)
        {
            return;
        }
        for (int i = 0; i < _line.Responses.Count; i++)
        {
            _responses.AddChild(new RichTextLabel
            {
                FitContent = true,
                BbcodeEnabled = true,
                Text =
                    (i == _response ? "> [u]" : "[color=transparent]>[/color] ") +
                    _line.Responses[i].Text +
                    (i == _response ? "[/u]" : ""),
                Modulate = _line.Responses[i].IsAllowed ? Colors.White : Colors.Gray,
            });
        }
    }

    /// <summary>
    /// Shows the box's contents while a dialogue is presented, so that a cutscene with no dialogue shows no box. The
    /// next dialogue starts in the same tick as the one before ends, so the box stays up between them.
    /// </summary>
    private void UpdateShown() => _shown.Visible = IsPresenting;

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible || !_shown.Visible)
        {
            return;
        }
        if (@event.IsActionPressed("jump"))
        {
            GetViewport().SetInputAsHandled();
            Advance();
        }
        else if (@event.IsActionPressed("move_up") || @event.IsActionPressed("move_down"))
        {
            GetViewport().SetInputAsHandled();
            ChooseResponse(@event.IsActionPressed("move_up") ? -1 : 1);
        }
    }

    /// <summary>
    /// Skips the typing of the line shown, running its inline mutations that are left; or once it has typed out, goes
    /// on from it: to the next line, or the chosen response's, if it is allowed, which is fetched in the world's next
    /// tick. Does nothing while no line is shown.
    /// </summary>
    public void Advance()
    {
        if (_line is null)
        {
            return;
        }
        if (!_typedOut)
        {
            _text.SkipTyping();
        }
        else if (_line.Responses.Count == 0)
        {
            _next = _line.NextId;
        }
        else if (_line.Responses[_response].IsAllowed)
        {
            _next = _line.Responses[_response].NextId;
        }
    }

    /// <summary>Chooses the response <paramref name="step"/> after the chosen one, wrapping around.</summary>
    private void ChooseResponse(int step)
    {
        if (_line is null || _line.Responses.Count == 0 || !_typedOut)
        {
            return;
        }
        _response = Mathf.PosMod(_response + step, _line.Responses.Count);
        ShowResponses();
    }
}
