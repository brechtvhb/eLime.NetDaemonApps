using Microsoft.Extensions.Logging;

namespace eLime.NetDaemonApps.Domain.SmartWashers.States;

public class SpinningState : SmartWasherState
{
    internal static readonly TimeSpan EstimatedDuration = TimeSpan.FromMinutes(15);
    private readonly TimeSpan maxDuration = TimeSpan.FromMinutes(20);

    internal override void Enter(ILogger logger, SmartWasher context)
    {
        return;
    }

    internal override void PowerUsageChanged(ILogger logger, SmartWasher context)
    {
        if (context.PowerSensor.State < 5)
            context.TransitionTo(logger, new ReadyState());

        if (context.LastStateChange.Add(maxDuration) < DateTime.Now)
            context.TransitionTo(logger, new ReadyState());
    }

    internal override DateTime? GetEta(ILogger logger, SmartWasher context)
    {
        return context.LastStateChange.Add(EstimatedDuration);
    }
}