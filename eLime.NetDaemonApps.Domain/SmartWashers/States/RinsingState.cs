using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SmartWashers.States;

public class RinsingState : SmartWasherState
{
    internal static readonly TimeSpan EstimatedDuration = TimeSpan.FromMinutes(45);

    private readonly TimeSpan minDuration = TimeSpan.FromMinutes(30);
    private readonly TimeSpan maxDuration = TimeSpan.FromMinutes(60);
    internal override void Enter(ILogger logger, IScheduler scheduler, SmartWasher context)
    {
        return;
    }

    internal override void PowerUsageChanged(ILogger logger, IScheduler scheduler, SmartWasher context)
    {
        if (context.LastStateChange.Add(minDuration) > scheduler.Now)
            return;

        if (context.PowerSensor.State > 300)
            context.TransitionTo(logger, new SpinningState());

        if (context.LastStateChange.Add(maxDuration) < scheduler.Now)
            context.TransitionTo(logger, new SpinningState());
    }

    internal override DateTimeOffset? GetEta(ILogger logger, SmartWasher context)
    {
        var nextStateEstimations = SpinningState.EstimatedDuration;
        return context.LastStateChange.Add(EstimatedDuration + nextStateEstimations);
    }
}