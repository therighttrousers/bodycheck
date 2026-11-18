using System.Collections.Generic;

using Godot;

namespace BodyCheck.Look;

/// <summary>
/// Advances every animation in steps, as games of the mid-1990s did, which stored few poses and didn't blend between
/// them. Steps are counted in real time, so that slow motion doesn't make animations choppier. Bodies still move
/// smoothly: only poses are stepped. All animations step together. This stops with the tree when it's paused, as
/// animations did before.
/// </summary>
public partial class AnimationStepper : Node
{
    private const float StepsPerSecond = 10;

    private readonly List<AnimationMixer> _mixers = [];

    private float _realTime;
    private float _gameTime;

    /// <summary>Whether animations advance in steps, and not every frame.</summary>
    public bool Enabled { get; set; }

    public override void _EnterTree() => GetTree().NodeAdded += OnNodeAdded;

    public override void _ExitTree() => GetTree().NodeAdded -= OnNodeAdded;

    public override void _Process(double delta)
    {
        _gameTime += (float)delta;
        _realTime += (float)(delta / Engine.TimeScale);
        if (Enabled && _realTime < 1 / StepsPerSecond)
        {
            return;
        }

        _mixers.RemoveAll(mixer => !IsInstanceValid(mixer));
        foreach (AnimationMixer mixer in _mixers)
        {
            if (mixer.IsInsideTree())
            {
                mixer.Advance(_gameTime);
            }
        }
        _gameTime = 0;
        _realTime %= 1 / StepsPerSecond;
    }

    private void OnNodeAdded(Node node)
    {
        if (node is AnimationMixer mixer)
        {
            mixer.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual;
            _mixers.Add(mixer);
        }
    }
}
