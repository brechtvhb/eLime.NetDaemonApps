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
                context.TransitionTo(logger, new PreWashingState());
        }

        internal override DateTime? GetEta(ILogger logger, SmartWasher context)
        {
            return null;
        }
    }
}
