using System;

using BodyCheck.Core;
using BodyCheck.Core.Controllers;
using BodyCheck.Core.Entities;
using BodyCheck.Core.Scripts;
using BodyCheck.Core.Tasks;

namespace BodyCheck.Scripts;

public class RunRightOffLedge(PlayerEntity player) : IScript
{
    public async TickTask Run(IScriptWorld world)
    {
        // Go into cutscene mode for the duration of this method.
        using IDisposable cutscene = world.HoldCutscene(new() { BlackBars = true });

        var controller = (PlayerController)player.Controller!;

        // Hold right until the ground falls away.
        controller.ScriptedInput = new PlayerInput(XAxis: 1);
        await world.Tick.While(() => player.OnGround);

        // Try to zero out horizontal velocity until landing. This is not as elegant as a PD controller, but any
        // overcorrection is barely visible.
        await world.Tick.While(
            () => !player.OnGround,
            () => controller.ScriptedInput = new PlayerInput(XAxis: Math.Sign(-player.LinearVelocity.X))
        );
    }
}
