using Microsoft.Extensions.Logging;

namespace eLime.NetDaemonApps.Domain.SmartWashers.States;

public class WashingState : SmartWasherState
{
    internal static TimeSpan EstimatedDuration = TimeSpan.FromMinutes(5);

    private readonly TimeSpan maxDuration = TimeSpan.FromMinutes(15);
    private DateTime? aboveThresholdSince = null;
    internal override void Enter(ILogger logger, SmartWasher context)
    {
        return;
    }

    internal override void PowerUsageChanged(ILogger logger, SmartWasher context)
    {
        if (context.PowerSensor.State < 30)
        {
            aboveThresholdSince = null;
            return;
        }

        if (context.PowerSensor.State >= 30 && aboveThresholdSince == null)
            aboveThresholdSince = DateTime.Now;

        if (aboveThresholdSince.HasValue && aboveThresholdSince.Value.Add(TimeSpan.FromSeconds(30)) < DateTime.Now)
            context.TransitionTo(logger, new RinsingState());

        if (context.LastStateChange.Add(maxDuration) < DateTime.Now)
            context.TransitionTo(logger, new RinsingState());
    }

    internal override DateTime? GetEta(ILogger logger, SmartWasher context)
    {
        var nextStateEstimations = RinsingState.EstimatedDuration + SpinningState.EstimatedDuration;
        return context.LastStateChange.Add(EstimatedDuration + nextStateEstimations);
    }
}