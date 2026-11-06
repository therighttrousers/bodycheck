using System;
using System.Collections.Generic;

using BodyCheck.Core.Scripts;
using BodyCheck.Core.Tasks;

using DialogueManagerRuntime;

using Godot;

namespace BodyCheck;

/// <summary>
/// The game's <see cref="IDialoguePresenter"/>, and a placeholder text box that shows the dialogue, in Godot's default
/// font, from Dialogue Manager's files in <see cref="DialogueDirectory"/>. It belongs in the game's viewport, on a layer
/// above the colour pass, along its bottom edge.
/// <para>
/// <see cref="Present"/> only queues the dialogue: <see cref="GameModeManager"/> starts each with
/// <see cref="StartNext"/> at its own point in the tick, once it has paused the game for it, and goes back to playing
/// once <see cref="IsPresenting"/> is false and nothing waits. Jump goes on to the next line, and up and down choose a
/// response. When a dialogue ends, its task finishes, and what waits for it resumes in the next played tick.
/// </para>
/// <para>
/// Dialogue Manager runs on Godot's frames and .NET's <c>Task</c>s, not on the world's tick: it fetches each line a
/// frame or more after it is asked. That is fine for the box, which only draws, while the world is paused; only the
/// end of a dialogue reaches the world, through a <see cref="TickCompletion"/>. The garble isn't drawn yet.
/// </para>
/// </summary>
public partial class DialogueBox : CanvasLayer, IDialoguePresenter
{
    private const string DialogueDirectory = "res://dialogue";
    private const string DialogueExtension = ".dialogue";

    private readonly Queue<Request> _waiting = new();
    private Request? _current;
    private Resource? _resource;
    // The line being shown, or null while the next is being fetched.
    private DialogueLine? _line;
    private int _response;

    private readonly Label _character = new();
    private readonly Label _text = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly VBoxContainer _responses = new();

    public DialogueBox()
    {
        Layer = 2; // Above the colour pass, so the dither leaves it alone.
        Visible = false;

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        margin.GrowVertical = Control.GrowDirection.Begin;
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

        var lines = new VBoxContainer();
        lines.AddThemeConstantOverride("separation", 0);
        panel.AddChild(lines);
        _character.Modulate = new Color(1, 0.85f, 0.4f);
        lines.AddChild(_character);
        lines.AddChild(_text);
        lines.AddChild(_responses);
    }

    /// <summary>Whether a dialogue waits for <see cref="StartNext"/>.</summary>
    public bool HasWaiting => _waiting.Count > 0;

    /// <summary>Whether a dialogue has started and not yet ended.</summary>
    public bool IsPresenting => _current is not null;

    public override void _Ready() => ProcessMode = ProcessModeEnum.Always;

    /// <inheritdoc/>
    public TickTask Present(ITick tick, string file, string title, float garble)
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
        string path = $"{DialogueDirectory}/{_current.File}{DialogueExtension}";
        _resource = ResourceLoader.Exists(path) ? GD.Load<Resource>(path) : null;
        if (_resource is null)
        {
            End(new System.IO.FileNotFoundException($"There is no dialogue file {path}."));
            return;
        }
        Fetch(_current.Title);
    }

    /// <summary>Fetches the line <paramref name="key"/> names, and shows it, or ends the dialogue if there is none.</summary>
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
        _line = line;
        _response = 0;
        ShowLine();
    }

    private void ShowLine()
    {
        _character.Text = _line!.Character;
        _character.Visible = _line.Character != "";
        _text.Text = _line.Text;
        foreach (Node child in _responses.GetChildren())
        {
            child.QueueFree();
        }
        for (int i = 0; i < _line.Responses.Count; i++)
        {
            _responses.AddChild(new Label
            {
                Text = (i == _response ? "> " : "  ") + _line.Responses[i].Text,
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
    /// Goes on from the line shown: to the next line, or the chosen response's, if it is allowed. Does nothing while
    /// no line is shown.
    /// </summary>
    public void Advance()
    {
        if (_line is null)
        {
            return;
        }
        if (_line.Responses.Count == 0)
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
        if (_line is null || _line.Responses.Count == 0)
        {
            return;
        }
        _response = Mathf.PosMod(_response + step, _line.Responses.Count);
        ShowLine();
    }

    private sealed record Request(string File, string Title, TickCompletion Completion);
}
