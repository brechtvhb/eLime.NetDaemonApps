using Microsoft.Extensions.Logging;

namespace eLime.NetDaemonApps.Domain.SmartWashers.States;

public class PreWashingState : SmartWasherState
{
    internal static readonly TimeSpan EstimatedDuration = TimeSpan.FromMinutes(10);
    private readonly TimeSpan maxDuration = TimeSpan.FromMinutes(15);
    internal override void Enter(ILogger logger, SmartWasher context)
    {
        context.SetWasherProgram(WasherProgram.Unknown);
    }

    internal override void PowerUsageChanged(ILogger logger, SmartWasher context)
    {

        if (context.PowerSensor.State > 500)
            context.TransitionTo(logger, new HeatingState());

        if (context.LastStateChange.Add(maxDuration) < DateTime.Now)
            context.TransitionTo(logger, new HeatingState());
    }

    internal override DateTime? GetEta(ILogger logger, SmartWasher context)
    {
        var nextStateEstimations = WashingState.EstimatedDuration + RinsingState.EstimatedDuration + SpinningState.EstimatedDuration;
        return context.LastStateChange.Add(EstimatedDuration + nextStateEstimations);
    }
}