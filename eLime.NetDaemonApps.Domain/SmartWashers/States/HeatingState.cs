using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SmartWashers.States;

public class HeatingState : SmartWasherState
{
    internal static TimeSpan EstimatedDuration = TimeSpan.FromMinutes(20);

    private readonly TimeSpan minDuration = TimeSpan.FromMinutes(7);
    private readonly TimeSpan maxDuration = TimeSpan.FromMinutes(30);
    private DateTimeOffset? startedSince;

    internal override void Enter(ILogger logger, IScheduler scheduler, SmartWasher context)
    {
        startedSince = scheduler.Now;
    }

    internal override void PowerUsageChanged(ILogger logger, IScheduler scheduler, SmartWasher context)
    {
        if (context.LastStateChange?.Add(minDuration) > scheduler.Now)
            return;

        if (startedSince != null && startedSince.Value.AddMinutes(15) < scheduler.Now)
        {
            context.SetWasherProgram(logger, WasherProgram.Wash60Degrees);
            EstimatedDuration = TimeSpan.FromMinutes(25);
        }

        if (context.PowerSensor.State < 20)
        {
            if (context.Program == WasherProgram.Unknown)
                context.SetWasherProgram(logger, WasherProgram.Wash40Degrees);

            logger.LogDebug("{SmartWasher}: Will transition to washing state because low power usage was detected", context.Name);
            context.TransitionTo(logger, new WashingState());
            return;
        }

        if (context.LastStateChange?.Add(maxDuration) < scheduler.Now)
        {
            logger.LogDebug("{SmartWasher}: Will transition to washing state because max duration elapsed.", context.Name);
            context.TransitionTo(logger, new WashingState());
        }
    }

    internal override DateTimeOffset? GetEta(ILogger logger, SmartWasher context)
    {
        var nextStateEstimations = WashingState.EstimatedDuration + RinsingState.EstimatedDuration + SpinningState.EstimatedDuration;
        return context.LastStateChange?.Add(EstimatedDuration + nextStateEstimations);
    }
}