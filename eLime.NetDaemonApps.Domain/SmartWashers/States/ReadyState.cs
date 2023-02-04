using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SmartWashers.States;

public class ReadyState : SmartWasherState
{
    internal override void Enter(ILogger logger, IScheduler scheduler, SmartWasher context)
    {
        context.SetWasherProgram(null);
    }

    internal override void PowerUsageChanged(ILogger logger, IScheduler scheduler, SmartWasher context)
    {
        switch (context.PowerSensor.State)
        {
            case < 1:
                context.TransitionTo(logger, new IdleState());
                return;
            case > 5 when !context.IsDelayedStartEnabled():
                context.TransitionTo(logger, new PreWashingState());
                return;
            case > 5:
                context.TransitionTo(logger, new DelayedStartState());
                break;
        }
    }

    internal override DateTimeOffset? GetEta(ILogger logger, SmartWasher context)
    {
        return context.LastStateChange;
    }
}