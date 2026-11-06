using System;

using BodyCheck.Core.Scripts;
using BodyCheck.Core.Tasks;

namespace BodyCheck.Scripts;

public class RunRightOffLedge : IScript
{
    public async TickTask Run(IScriptWorld world, ITick tick)
    {
        // TODO (next commit): detect going off the ledge and landing, and stop when landed.
        using IDisposable cutscene = world.HoldCutscene();
        world.PlayerController.ScriptedInput = new Core.PlayerInput(XAxis: 1);
        await tick.Wait(1); // HACK
    }
}
