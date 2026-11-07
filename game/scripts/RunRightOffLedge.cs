using System;

using BodyCheck.Core;
using BodyCheck.Core.Scripts;
using BodyCheck.Core.Tasks;

namespace BodyCheck.Scripts;

public class RunRightOffLedge : IScript
{
    public async TickTask Run(IScriptWorld world, ITick tick)
    {
        // Go into cutscene mode for the duration of this method.
        using IDisposable cutscene = world.HoldCutscene(new() { BlackBars = true });

        // Hold right until the ground falls away.
        world.PlayerController.ScriptedInput = new PlayerInput(XAxis: 1);
        await tick.While(() => world.Player.OnGround);

        // Try to zero out horizontal velocity until landing. This is not as elegant as a PD controller, but any
        // overcorrection is barely visible.
        await tick.While(
            () => !world.Player.OnGround,
            () => world.PlayerController.ScriptedInput = new PlayerInput(XAxis: Math.Sign(-world.Player.LinearVelocity.X))
        );
    }
}
