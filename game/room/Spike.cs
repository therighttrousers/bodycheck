using Godot;

namespace BodyCheck;

/// <summary>
/// Switches for the spike of decision record 005: traction as a motor inside the engine's solver, and
/// drag as the engine's damping. Set by "--traction=motor" and "--damping=engine" after "--" on the
/// command line, or by the environment variables BODYCHECK_TRACTION=motor and BODYCHECK_DAMPING=engine.
/// </summary>
public static class Spike
{
    public static bool MotorTraction { get; set; } = OS.GetEnvironment("BODYCHECK_TRACTION") == "motor";

    public static bool EngineDamping { get; set; } = OS.GetEnvironment("BODYCHECK_DAMPING") == "engine";

    /// <summary>Which way the joint's motor counts a positive target velocity. Found by measurement.</summary>
    public static float MotorSign { get; set; } = OS.GetEnvironment("BODYCHECK_MOTOR_SIGN") == "-1" ? -1 : 1;
}
