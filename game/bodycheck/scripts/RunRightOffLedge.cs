using System;

using BodyCheck.Core;
using BodyCheck.Core.Controllers;
using BodyCheck.Core.Entities;
using BodyCheck.Core.Scripts;
using BodyCheck.Core.Tasks;

namespace BodyCheck.Scripts;

public class RunRightOffLedge : IScript
{
    // Game seconds. The run and fall take about 1.5. If the player starts where it can't run off a ledge, the script
    // ends anyway, and lets go of the cutscene, rather than holding it forever.
    private const double Timeout = 2;

    public async TickTask Run(IScriptWorld world)
    {
        // Go into cutscene mode for the duration of this method.
        using IDisposable cutscene = world.HoldCutscene(new() { BlackBars = true });

        double endTime = world.Tick.Time + Timeout;
        ITick tick = world.Tick.Scoped(() => world.Tick.Time < endTime);

        PlayerEntity player = world.Player;
        PlayerController controller = world.PlayerController;

        // Hold right until the ground falls away.
        controller.ScriptedInput = new PlayerInput(XAxis: 1);
        await tick.While(() => player.OnGround);

        // Fall with no input until landing. The camera's lead holds, which hints to the user where to go next: right.
        controller.ScriptedInput = new PlayerInput();
        await tick.While(() => !player.OnGround);
    }
}
