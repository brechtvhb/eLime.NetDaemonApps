﻿using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SmartWashers.States;

public class WashingState : SmartWasherState
{
    internal static TimeSpan EstimatedDuration = TimeSpan.FromMinutes(5);

    private readonly TimeSpan minDuration = TimeSpan.FromMinutes(3);
    private readonly TimeSpan maxDuration = TimeSpan.FromMinutes(15);
    private DateTimeOffset? aboveThresholdSince = null;
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
            logger.LogDebug("{SmartWasher}: Will transition to rinsing state because max duration elapsed.", context.Name);
            context.TransitionTo(logger, new RinsingState());
            return;
        }

        if (context.PowerSensor.State < 30)
        {
            aboveThresholdSince = null;
            return;
        }

        if (context.PowerSensor.State >= 30 && aboveThresholdSince == null)
            aboveThresholdSince = scheduler.Now;

        if (aboveThresholdSince.HasValue && aboveThresholdSince.Value.Add(TimeSpan.FromSeconds(30)) < scheduler.Now)
        {
            logger.LogDebug("{SmartWasher}: Will transition to rinsing state because high power usage was detected in the last 30 seconds.", context.Name);
            context.TransitionTo(logger, new RinsingState());
        }
    }

    internal override DateTimeOffset? GetEta(ILogger logger, SmartWasher context)
    {
        var nextStateEstimations = RinsingState.EstimatedDuration + SpinningState.EstimatedDuration;
        return context.LastStateChange?.Add(EstimatedDuration + nextStateEstimations);
    }
}