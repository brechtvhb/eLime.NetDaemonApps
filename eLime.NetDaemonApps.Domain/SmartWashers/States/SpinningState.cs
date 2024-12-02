using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SmartWashers.States;

public class SpinningState : SmartWasherState
{
    internal static readonly TimeSpan EstimatedDuration = TimeSpan.FromMinutes(15);

    private readonly TimeSpan minDuration = TimeSpan.FromMinutes(5);
    private readonly TimeSpan maxDuration = TimeSpan.FromMinutes(25);
    private DateTimeOffset? belowThresholdSince = null;

    internal override void Enter(ILogger logger, IScheduler scheduler, SmartWasher context)
    {
        return;
    }

    internal override void PowerUsageChanged(ILogger logger, IScheduler scheduler, SmartWasher context)
    {
        context.CalculateProgress();

        if (context.LastStateChange?.Add(minDuration) > scheduler.Now)
            return;

        if (context.LastStateChange?.Add(maxDuration) < scheduler.Now)
        {
            logger.LogDebug("{SmartWasher}: Will transition to ready state because max duration elapsed.", context.Name);
            context.TransitionTo(logger, new ReadyState());
            return;
        }

        if (context.PowerSensor.State > 5)
        {
            belowThresholdSince = null;
            return;
        }

        if (context.PowerSensor.State < 5 && belowThresholdSince == null)
            belowThresholdSince = scheduler.Now;

        if (belowThresholdSince.HasValue && belowThresholdSince.Value.Add(TimeSpan.FromSeconds(15)) < scheduler.Now)
        {
            logger.LogDebug("{SmartWasher}: Will transition to ready state because low power usage was detected in the last 10 seconds.", context.Name);
            context.TransitionTo(logger, new ReadyState());
        }
    }

    internal override DateTimeOffset? GetEta(ILogger logger, SmartWasher context)
    {
        return context.LastStateChange?.Add(EstimatedDuration);
    }
}