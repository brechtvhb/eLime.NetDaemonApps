using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SmartWashers.States;

public class DelayedStartState : SmartWasherState
{
    internal override void Enter(ILogger logger, IScheduler scheduler, SmartWasher context)
    {
        context.TurnPowerSocketOFf();
    }

    internal void Start(ILogger logger, SmartWasher context)
    {
        context.TransitionTo(logger, new PreWashingState());
    }
    internal override void PowerUsageChanged(ILogger logger, IScheduler scheduler, SmartWasher context)
    {

    }

    internal override DateTimeOffset? GetEta(ILogger logger, SmartWasher context)
    {
        return null;
    }
}