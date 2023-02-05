using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SmartWashers.States;

public class IdleState : SmartWasherState
{
    private DateTimeOffset? aboveThresholdSince = null;

    internal override void Enter(ILogger logger, IScheduler scheduler, SmartWasher context)
    {
        return;
    }

    internal override void PowerUsageChanged(ILogger logger, IScheduler scheduler, SmartWasher context)
    {
        switch (context.PowerSensor.State)
        {
            case < 5:
                aboveThresholdSince = null;
                break;
            case > 5 when aboveThresholdSince == null:
                aboveThresholdSince = scheduler.Now;
                break;
        }

        if (!aboveThresholdSince.HasValue || aboveThresholdSince.Value.Add(TimeSpan.FromSeconds(15)) >= scheduler.Now)
            return;

        if (context.IsDelayedStartEnabled())
        {
            context.TransitionTo(logger, new DelayedStartState());
            return;
        }

        context.TransitionTo(logger, new PreWashingState());
    }

    internal override DateTimeOffset? GetEta(ILogger logger, SmartWasher context)
    {
        return null;
    }
}