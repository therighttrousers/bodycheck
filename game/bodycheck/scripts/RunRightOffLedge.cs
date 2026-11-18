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

        // Fall with no input until landing. The camera's lead holds, which hints to the user where to go next: right.
        controller.ScriptedInput = new PlayerInput();
        await world.Tick.While(() => !player.OnGround);
    }
}
