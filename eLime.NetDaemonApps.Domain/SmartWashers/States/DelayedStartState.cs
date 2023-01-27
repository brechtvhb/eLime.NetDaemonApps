using Microsoft.Extensions.Logging;

namespace eLime.NetDaemonApps.Domain.SmartWashers.States;

public class DelayedStartState : SmartWasherState
{
    internal override void Enter(ILogger logger, SmartWasher context)
    {
        context.TurnPowerSocketOFf();
    }

    internal void Start(ILogger logger, SmartWasher context)
    {
        context.TransitionTo(logger, new PreWashingState());
    }
    internal override void PowerUsageChanged(ILogger logger, SmartWasher context)
    {

    }

    internal override DateTime? GetEta(ILogger logger, SmartWasher context)
    {
        return null;
    }
}