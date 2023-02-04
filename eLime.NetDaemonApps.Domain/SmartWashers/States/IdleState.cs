using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SmartWashers.States
{
    public class IdleState : SmartWasherState
    {
        internal override void Enter(ILogger logger, IScheduler scheduler, SmartWasher context)
        {
            return;
        }

        internal override void PowerUsageChanged(ILogger logger, IScheduler scheduler, SmartWasher context)
        {
            switch (context.PowerSensor.State)
            {
                case > 5 when !context.IsDelayedStartEnabled():
                    context.TransitionTo(logger, new PreWashingState());
                    return;
                case > 5:
                    context.TransitionTo(logger, new DelayedStartState());
                    break;
            }
        }

        internal override DateTimeOffset? GetEta(ILogger logger, SmartWasher context)
        {
            return null;
        }
    }
}
