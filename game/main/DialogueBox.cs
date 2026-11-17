using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using BodyCheck.Core;
using BodyCheck.Core.Entities;
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
/// <para>
/// A dialogue file sees a <see cref="DialogueState"/> while it is presented, as Dialogue Manager's extra game state:
/// the entities the dialogue is about, by name, and <see cref="DialogueState.Act"/>, which asks one for an action.
/// Dialogue Manager runs mutations off the tick, so an action asked for waits until the method's next check, which
/// starts it on the tick.
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
    // What the dialogue presented sees, or null while none is.
    private DialogueState? _state;
    // The actions the dialogue presented has asked for, off the tick, not yet started on it.
    private readonly List<AskedAction> _asked = [];

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

    /// <summary>
    /// The state of the world whose dialogues the box presents, for the names and kinds of the entities they are about.
    /// <see cref="Main"/> sets it for each world it builds.
    /// </summary>
    public WorldState? WorldState { get; set; }

    /// <inheritdoc/>
    public TickTask Present(ITick tick, string file, string title, IReadOnlyList<Entity> entities) =>
        _presenting.Run(() => Run(tick, file, title, entities));

    /// <summary>
    /// Presents the dialogue, line by line, until it ends, starting the actions it asks for on the tick. A missing file,
    /// a garble tag that isn't valid, an action that isn't one, or an error in Dialogue Manager fails it. However it
    /// ends, even by its tick being stopped, the box drops it.
    /// </summary>
    private async TickTask Run(ITick tick, string file, string title, IReadOnlyList<Entity> entities)
    {
        Resource resource = Load(file)
            ?? throw new System.IO.FileNotFoundException($"There is no dialogue file {PathOf(file)}.");
        _state = new DialogueState { Box = this, Entities = [.. entities.Select(Describe)] };
        Godot.Collections.Array<Variant> states = [_state];
        IsPresenting = true;
        UpdateShown();
        try
        {
            string key = title;
            while (true)
            {
                _line = null;
                Task<DialogueLine?> fetching = DialogueManager.GetNextDialogueLine(resource, key, states);
                // A mutation on its own line runs while the next line is fetched, and may wait for an action it asks for.
                await tick.While(() => !fetching.IsCompleted, () => StartAskedActions(tick));
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
                // An inline mutation runs as the line types out.
                await tick.While(() => _next is null, () => StartAskedActions(tick));
                key = _next!;
            }
        }
        finally
        {
            IsPresenting = false;
            _line = null;
            _next = null;
            _text.DialogueLine = null;
            _state = null;
            // Nothing will start them now, and what waits for them goes on, with nothing to act for.
            foreach (AskedAction asked in _asked)
            {
                asked.Done.TrySetResult();
            }
            _asked.Clear();
            ClearResponses();
            UpdateShown();
        }
    }

    /// <summary>What a dialogue sees of an entity it is about: its name and its kind's, from the world's state.</summary>
    private DialogueEntity Describe(Entity entity)
    {
        WorldState state = WorldState
            ?? throw new InvalidOperationException("A dialogue about entities is presented with no world state to name them.");
        WorldState.EntityState described = state.GetEntity(entity.Id);
        return new DialogueEntity { Entity = entity, Name = described.Name, Kind = WorldState.KindName(described.Kind) };
    }

    /// <summary>An action that a dialogue has asked an entity for, and what to complete when it has finished.</summary>
    private sealed record AskedAction(DialogueEntity Entity, string Action, TaskCompletionSource Done);

    /// <summary>
    /// Asks <paramref name="entity"/> for the action named <paramref name="action"/>, off the tick, as Dialogue Manager
    /// runs mutations: the presenting method starts it on the tick when it next checks. The task completes when the
    /// action finishes, fails or is cancelled, or the dialogue ends first.
    /// </summary>
    internal Task Ask(DialogueEntity entity, string action)
    {
        // So that Dialogue Manager goes on when its frames come round, not within the world's tick.
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (IsPresenting)
        {
            _asked.Add(new AskedAction(entity, action, done));
        }
        else
        {
            done.SetResult();
        }
        return done.Task;
    }

    /// <summary>Starts the actions asked for since the last check, on <paramref name="tick"/>.</summary>
    private void StartAskedActions(ITick tick)
    {
        List<AskedAction> asked = [.. _asked];
        _asked.Clear();
        foreach (AskedAction action in asked)
        {
            _ = RunAskedAction(tick, action);
        }
    }

    /// <summary>
    /// Runs an action a dialogue asked for, by its <see cref="TriggerAction"/>'s name, on the entity's controller. One
    /// that isn't an action, or an entity without a controller, fails the dialogue. A failure of the action itself is
    /// the world's, which reports what nothing awaits, and the dialogue goes on.
    /// </summary>
    private static async TickTask RunAskedAction(ITick tick, AskedAction asked)
    {
        try
        {
            if (!Enum.TryParse(asked.Action, out TriggerAction action) || !Enum.IsDefined(action))
            {
                throw new ArgumentException($"A dialogue asked {asked.Entity.Name} for {asked.Action}, which isn't an action.");
            }
            IActionTarget target = asked.Entity.Entity.Controller
                ?? throw new NotSupportedException($"A dialogue asked {asked.Entity.Name}, which has no controller, for {action}.");
            await new Call(target, action).Run(tick);
        }
        finally
        {
            asked.Done.TrySetResult();
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
        // The speaker's own avatar, or if the speaker is one of the entities the dialogue is about, its kind's.
        _avatar.Texture = LoadAvatar(_line.Character)
            ?? (_state?.Entities.FirstOrDefault(entity => entity.Name == _line.Character) is DialogueEntity speaker
                ? LoadAvatar(speaker.Kind)
                : null);
        _avatarBackground.Visible = _avatar.Texture is not null;
        _text.DialogueLine = _line;
        ClearResponses();
        _typedOut = false;
        _text.TypeOut();
    }

    /// <summary>The avatar named <paramref name="name"/>, or null if there is none.</summary>
    private static Texture2D? LoadAvatar(string name)
    {
        string path = $"{AvatarDirectory}/{name}.png";
        return name != "" && ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
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
