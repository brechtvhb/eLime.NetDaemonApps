using Microsoft.Extensions.Logging;

namespace eLime.NetDaemonApps.Domain.SmartWashers.States;

public class RinsingState : SmartWasherState
{
    internal static readonly TimeSpan EstimatedDuration = TimeSpan.FromMinutes(45);
    private readonly TimeSpan maxDuration = TimeSpan.FromMinutes(60);
    internal override void Enter(ILogger logger, SmartWasher context)
    {
        return;
    }

    internal override void PowerUsageChanged(ILogger logger, SmartWasher context)
    {
        if (context.PowerSensor.State > 300)
            context.TransitionTo(logger, new SpinningState());

        if (context.LastStateChange.Add(maxDuration) < DateTime.Now)
            context.TransitionTo(logger, new SpinningState());
    }

    internal override DateTime? GetEta(ILogger logger, SmartWasher context)
    {
        var nextStateEstimations = SpinningState.EstimatedDuration;
        return context.LastStateChange.Add(EstimatedDuration + nextStateEstimations);
    }
}