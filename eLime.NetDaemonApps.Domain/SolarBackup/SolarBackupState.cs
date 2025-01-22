using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SolarBackup
{
    public abstract class SolarBackupState
    {
        internal abstract void Enter(ILogger logger, IScheduler scheduler, SolarBackup context);
        internal abstract void CheckProgress(ILogger logger, IScheduler scheduler, SolarBackup context);

    }
}
