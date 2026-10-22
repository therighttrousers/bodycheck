using Godot;

namespace BodyCheck;

/// <summary>
/// Muffles every sound while the game slows down for a phase and while it is paused, with the first low-pass filter on
/// the master bus. Each frame, it sets the filter's cutoff from the <see cref="GameMode"/> and the engine's time scale:
/// open while playing; falling with the time scale while slowing for a phase, evenly in octaves; closed in every mode
/// that pauses. It keeps no state of its own, so it can't drift from the slowdown (decision record 021 §2.2).
/// <para>
/// The filter is tuned in the bus layout: its cutoff there is the closed one. It is off there, so that sounds aren't
/// muffled in the editor, and this turns it on only while muffling. With no such filter, this does nothing.
/// </para>
/// </summary>
public partial class Muffler : Node
{
    /// <summary>The cutoff, in hertz, with the filter open: the highest the engine allows.</summary>
    private const float OpenCutoff = 20500;

    private const int MasterBus = 0;

    private readonly GameModeManager _gameModeManager;
    private AudioEffectLowPassFilter? _filter;
    private int _filterIndex;
    private float _closedCutoff;

    public Muffler(GameModeManager gameModeManager)
    {
        _gameModeManager = gameModeManager;
    }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        for (int i = 0; i < AudioServer.GetBusEffectCount(MasterBus); i++)
        {
            if (AudioServer.GetBusEffect(MasterBus, i) is AudioEffectLowPassFilter filter)
            {
                _filter = filter;
                _filterIndex = i;
                _closedCutoff = filter.CutoffHz;
                return;
            }
        }
        GD.PushWarning("The master bus has no low-pass filter, so sound won't be muffled.");
    }

    public override void _Process(double delta)
    {
        if (_filter is null)
        {
            return;
        }

        // How far muffled, from 0, open, to 1, closed.
        float muffling = _gameModeManager.GameMode switch
        {
            GameMode.Playing => 0,
            GameMode.SlowingForPhase => (float)Mathf.Clamp(
                (1 - Engine.TimeScale) / (1 - GameModeManager.SlowestTimeScale), 0, 1),
            _ => 1,
        };
        AudioServer.SetBusEffectEnabled(MasterBus, _filterIndex, muffling > 0);
        _filter.CutoffHz = OpenCutoff * Mathf.Pow(_closedCutoff / OpenCutoff, muffling);
    }
}
