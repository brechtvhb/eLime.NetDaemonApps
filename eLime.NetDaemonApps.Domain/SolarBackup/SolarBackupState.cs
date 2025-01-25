using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SolarBackup
{
    public abstract class SolarBackupState
    {
        internal abstract Task Enter(ILogger logger, IScheduler scheduler, SolarBackup context);
        internal abstract Task CheckProgress(ILogger logger, IScheduler scheduler, SolarBackup context);

    }
}
