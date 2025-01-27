using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SolarBackup.States;

public class IdleState : SolarBackupState
{
    internal override Task Enter(ILogger logger, IScheduler scheduler, SolarBackup context)
    {
        return Task.CompletedTask;
    }

    internal override Task CheckProgress(ILogger logger, IScheduler scheduler, SolarBackup context)
    {
        return Task.CompletedTask;
    }
}