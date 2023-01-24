using Microsoft.Extensions.Logging;

namespace eLime.NetDaemonApps.Domain.SmartWashers.States;

public class ReadyState : SmartWasherState
{
    internal override void Enter(ILogger logger, SmartWasher context)
    {
        context.SetWasherProgram(null);
    }

    internal override void PowerUsageChanged(ILogger logger, SmartWasher context)
    {
        if (context.PowerSensor.State < 1)
            context.TransitionTo(logger, new IdleState());
    }

    internal override DateTime? GetEta(ILogger logger, SmartWasher context)
    {
        return context.LastStateChange;
    }
}