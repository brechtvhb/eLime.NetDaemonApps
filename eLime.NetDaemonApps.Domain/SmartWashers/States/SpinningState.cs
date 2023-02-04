using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SmartWashers.States;

public class SpinningState : SmartWasherState
{
    internal static readonly TimeSpan EstimatedDuration = TimeSpan.FromMinutes(15);

    private readonly TimeSpan minDuration = TimeSpan.FromMinutes(10);
    private readonly TimeSpan maxDuration = TimeSpan.FromMinutes(20);
    private DateTimeOffset? belowThresholdSince = null;

    internal override void Enter(ILogger logger, IScheduler scheduler, SmartWasher context)
    {
        return;
    }

    internal override void PowerUsageChanged(ILogger logger, IScheduler scheduler, SmartWasher context)
    {
        if (context.LastStateChange.Add(minDuration) > scheduler.Now)
            return;

        if (context.PowerSensor.State > 5)
        {
            belowThresholdSince = null;
            return;
        }

        if (context.PowerSensor.State < 5 && belowThresholdSince == null)
            belowThresholdSince = scheduler.Now;

        if (belowThresholdSince.HasValue && belowThresholdSince.Value.Add(TimeSpan.FromSeconds(60)) < scheduler.Now)
            context.TransitionTo(logger, new ReadyState());

        if (context.LastStateChange.Add(maxDuration) < scheduler.Now)
            context.TransitionTo(logger, new ReadyState());
    }

    internal override DateTimeOffset? GetEta(ILogger logger, SmartWasher context)
    {
        return context.LastStateChange.Add(EstimatedDuration);
    }
}