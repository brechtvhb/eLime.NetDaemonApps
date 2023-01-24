using Microsoft.Extensions.Logging;

namespace eLime.NetDaemonApps.Domain.SmartWashers
{
    public abstract class SmartWasherState
    {
        internal abstract void Enter(ILogger logger, SmartWasher context);
        internal abstract void PowerUsageChanged(ILogger logger, SmartWasher context);

        internal abstract DateTime? GetEta(ILogger logger, SmartWasher context);
    }
}
