using System;

using BodyCheck.Core;
using BodyCheck.Core.Scripts;
using BodyCheck.Core.Tasks;

namespace BodyCheck.Scripts;

public class RunRightOffLedge : IScript
{
    public async TickTask Run(IScriptWorld world, ITick tick)
    {
        using IDisposable cutscene = world.HoldCutscene(new() { BlackBars = true });
        // Hold right until the ground falls away.
        world.PlayerController.ScriptedInput = new PlayerInput(XAxis: 1);
        while (world.Player.OnGround) await tick.NextTick();
        // Try to zero out horizontal velocity until landing.
        while (!world.Player.OnGround)
        {
            // Not as elegant as a PD controller, but any overcorrection is barely visible.
            world.PlayerController.ScriptedInput = new PlayerInput(XAxis: Math.Sign(-world.Player.LinearVelocity.X));
            await tick.NextTick();
        }
    }
}
