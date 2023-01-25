using Microsoft.Extensions.Logging;

namespace eLime.NetDaemonApps.Domain.SmartWashers.States;

public class HeatingState : SmartWasherState
{
    internal static TimeSpan EstimatedDuration = TimeSpan.FromMinutes(20);

    private readonly TimeSpan maxDuration = TimeSpan.FromMinutes(30);
    private DateTime? startedSince;

    internal override void Enter(ILogger logger, SmartWasher context)
    {
        startedSince = DateTime.Now;
    }

    internal override void PowerUsageChanged(ILogger logger, SmartWasher context)
    {
        if (startedSince != null && startedSince.Value.AddMinutes(15) < DateTime.Now)
        {
            context.SetWasherProgram(WasherProgram.Wash60Degrees);
            EstimatedDuration = TimeSpan.FromMinutes(25);
        }

        if (context.PowerSensor.State < 20)
        {
            if (context.Program == WasherProgram.Unknown)
                context.SetWasherProgram(WasherProgram.Wash40Degrees);

            context.TransitionTo(logger, new WashingState());
        }

        if (context.LastStateChange.Add(maxDuration) < DateTime.Now)
            context.TransitionTo(logger, new WashingState());
    }

    internal override DateTime? GetEta(ILogger logger, SmartWasher context)
    {
        var nextStateEstimations = WashingState.EstimatedDuration + RinsingState.EstimatedDuration + SpinningState.EstimatedDuration;
        return context.LastStateChange.Add(EstimatedDuration + nextStateEstimations);
    }
}