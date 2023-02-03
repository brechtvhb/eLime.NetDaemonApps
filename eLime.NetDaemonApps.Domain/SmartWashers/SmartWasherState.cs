using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SmartWashers
{
    public abstract class SmartWasherState
    {
        internal abstract void Enter(ILogger logger, IScheduler scheduler, SmartWasher context);
        internal abstract void PowerUsageChanged(ILogger logger, IScheduler scheduler, SmartWasher context);

        internal abstract DateTimeOffset? GetEta(ILogger logger, SmartWasher context);
    }
}
