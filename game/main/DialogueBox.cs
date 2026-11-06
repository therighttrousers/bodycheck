using System;
using System.Collections.Generic;
using System.Linq;

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
/// have one in <see cref="AvatarDirectory"/>. The box itself is still a placeholder.
/// <para>
/// <see cref="Present"/> only queues the dialogue: <see cref="GameModeManager"/> starts each with
/// <see cref="StartNext"/> at its own point in the tick, once it has paused the game for it, and goes back to playing
/// once <see cref="IsPresenting"/> is false and nothing waits. Each line types out, and its responses show once it
/// has. Jump skips the typing, or once a line has typed out, goes on to the next, and up and down choose a response.
/// When a dialogue ends, its task finishes, and what waits for it resumes in the next played tick.
/// </para>
/// <para>
/// Dialogue Manager runs on Godot's frames and .NET's <c>Task</c>s, not on the world's tick: it fetches each line a
/// frame or more after it is asked. That is fine for the box, which only draws, while the world is paused; only the
/// end of a dialogue reaches the world, through a <see cref="TickCompletion"/>.
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

    private readonly Queue<Request> _waiting = new();
    private Request? _current;
    private Resource? _resource;
    // The line being shown, or null while the next is being fetched.
    private DialogueLine? _line;
    private int _response;

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

    public DialogueBox()
    {
        Layer = 2; // Above the colour pass, so the dither leaves it alone.
        Visible = false;

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        margin.GrowVertical = Control.GrowDirection.Begin;
        // Every line's height comes from the theme's font, so that a locale's font sets it.
        margin.Theme = GD.Load<Theme>(ThemePath);
        foreach (string side in new[] { "left", "right", "bottom" })
        {
            margin.AddThemeConstantOverride($"margin_{side}", 4);
        }
        AddChild(margin);

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
        margin.AddChild(panel);

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
        _text.FinishedTyping += ShowResponses;
    }

    /// <summary>Whether a dialogue waits for <see cref="StartNext"/>.</summary>
    public bool HasWaiting => _waiting.Count > 0;

    /// <summary>Whether a dialogue has started and not yet ended.</summary>
    public bool IsPresenting => _current is not null;

    public override void _Ready() => ProcessMode = ProcessModeEnum.Always;

    /// <inheritdoc/>
    public TickTask Present(ITick tick, string file, string title)
    {
        TickCompletion completion = tick.NewCompletion();
        _waiting.Enqueue(new Request(file, title, completion));
        return completion.Task;
    }

    /// <summary>Starts the dialogue that has waited longest. Only one is presented at a time.</summary>
    public void StartNext()
    {
        if (_current is not null)
        {
            throw new InvalidOperationException("A dialogue is already being presented.");
        }
        _current = _waiting.Dequeue();
        _resource = Load(_current.File);
        if (_resource is null)
        {
            End(new System.IO.FileNotFoundException($"There is no dialogue file {PathOf(_current.File)}."));
            return;
        }
        Fetch(_current.Title);
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

    /// <summary>
    /// Fetches the line <paramref name="key"/> names, and shows it, garbled as its tag says, or ends the dialogue if
    /// there is none. A garble tag that isn't valid fails the dialogue.
    /// </summary>
    private async void Fetch(string key)
    {
        Request request = _current!;
        _line = null;
        DialogueLine? line;
        try
        {
            line = await DialogueManager.GetNextDialogueLine(_resource!, key);
        }
        catch (Exception e)
        {
            End(e);
            return;
        }
        if (_current != request)
        {
            return; // Ended while the line was fetched.
        }
        if (line is null)
        {
            End(null);
            return;
        }
        try
        {
            _text.GarbleFraction = Garble.FractionOf(line.Tags);
        }
        catch (FormatException e)
        {
            End(new FormatException($"In the dialogue {request.File}, title {request.Title}: {e.Message}", e));
            return;
        }
        _line = line;
        _response = 0;
        ShowLine();
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

    /// <summary>Ends the dialogue being presented, finishing its task, or failing it with <paramref name="failure"/>.</summary>
    private void End(Exception? failure)
    {
        Request request = _current!;
        _current = null;
        _resource = null;
        _line = null;
        _text.DialogueLine = null;
        ClearResponses();
        if (failure is null)
        {
            request.Completion.Finish();
        }
        else
        {
            request.Completion.Fail(failure);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible)
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
    /// on from it: to the next line, or the chosen response's, if it is allowed. Does nothing while no line is shown.
    /// </summary>
    public void Advance()
    {
        if (_line is null)
        {
            return;
        }
        if (_text.IsTyping)
        {
            _text.SkipTyping();
        }
        else if (_line.Responses.Count == 0)
        {
            Fetch(_line.NextId);
        }
        else if (_line.Responses[_response].IsAllowed)
        {
            Fetch(_line.Responses[_response].NextId);
        }
    }

    /// <summary>Chooses the response <paramref name="step"/> after the chosen one, wrapping around.</summary>
    private void ChooseResponse(int step)
    {
        if (_line is null || _line.Responses.Count == 0 || _text.IsTyping)
        {
            return;
        }
        _response = Mathf.PosMod(_response + step, _line.Responses.Count);
        ShowResponses();
    }

    private sealed record Request(string File, string Title, TickCompletion Completion);
}
