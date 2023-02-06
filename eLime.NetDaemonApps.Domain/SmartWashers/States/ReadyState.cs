using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SmartWashers.States;

public class ReadyState : SmartWasherState
{
    private DateTimeOffset? aboveThresholdSince = null;
    internal override void Enter(ILogger logger, IScheduler scheduler, SmartWasher context)
    {
        context.SetWasherProgram(null);
    }

    internal override void PowerUsageChanged(ILogger logger, IScheduler scheduler, SmartWasher context)
    {
        switch (context.PowerSensor.State)
        {
            case < 1:
                logger.LogDebug("{SmartWasher}: Will transition to idle state because no power usage was detected", context.Name);
                context.TransitionTo(logger, new IdleState());
                break;
            case < 5:
                aboveThresholdSince = null;
                break;
            case > 5 when aboveThresholdSince == null:
                aboveThresholdSince = scheduler.Now;
                break;
        }

        if (!aboveThresholdSince.HasValue || aboveThresholdSince.Value.Add(TimeSpan.FromSeconds(10)) >= scheduler.Now)
            return;

        if (context.IsDelayedStartEnabled())
        {
            logger.LogDebug("{SmartWasher}: Will transition to delayed start state because delayed start switch is on.", context.Name);
            context.TransitionTo(logger, new DelayedStartState());
            return;
        }

        logger.LogDebug("{SmartWasher}: Will transition to pre washing state because delayed start switch is off.", context.Name);
        context.TransitionTo(logger, new PreWashingState());
    }

    internal override DateTimeOffset? GetEta(ILogger logger, SmartWasher context)
    {
        return context.LastStateChange;
    }
}