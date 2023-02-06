using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SmartWashers.States;

public class PreWashingState : SmartWasherState
{
    internal static readonly TimeSpan EstimatedDuration = TimeSpan.FromMinutes(10);
    private readonly TimeSpan minDuration = TimeSpan.FromMinutes(5);
    private readonly TimeSpan maxDuration = TimeSpan.FromMinutes(15);
    internal override void Enter(ILogger logger, IScheduler scheduler, SmartWasher context)
    {
        context.SetWasherProgram(WasherProgram.Unknown);
    }

    internal override void PowerUsageChanged(ILogger logger, IScheduler scheduler, SmartWasher context)
    {
        if (context.LastStateChange?.Add(minDuration) > scheduler.Now)
            return;

        if (context.PowerSensor.State > 500)
        {
            logger.LogDebug("{SmartWasher}: Will transition to heating state because high power usage was detected.", context.Name);
            context.TransitionTo(logger, new HeatingState());
        }

        if (context.LastStateChange?.Add(maxDuration) < scheduler.Now)
        {
            logger.LogDebug("{SmartWasher}: Will transition to heating state because max duration elapsed.", context.Name);
            context.TransitionTo(logger, new HeatingState());
        }
    }

    internal override DateTimeOffset? GetEta(ILogger logger, SmartWasher context)
    {
        var nextStateEstimations = WashingState.EstimatedDuration + RinsingState.EstimatedDuration + SpinningState.EstimatedDuration;
        return context.LastStateChange?.Add(EstimatedDuration + nextStateEstimations);
    }
}