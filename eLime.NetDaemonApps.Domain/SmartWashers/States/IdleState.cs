using Microsoft.Extensions.Logging;

namespace eLime.NetDaemonApps.Domain.SmartWashers.States
{
    public class IdleState : SmartWasherState
    {
        internal override void Enter(ILogger logger, SmartWasher context)
        {
            return;
        }

        internal override void PowerUsageChanged(ILogger logger, SmartWasher context)
        {
            //TODO: delayed start
            if (context.PowerSensor.State > 1)
            {
                if (!context.IsDelayedStartEnabled())
                {
                    context.TransitionTo(logger, new PreWashingState());
                    return;
                }

                context.TransitionTo(logger, new DelayedStartState());
            }
        }

        internal override DateTime? GetEta(ILogger logger, SmartWasher context)
        {
            return null;
        }
    }
}
