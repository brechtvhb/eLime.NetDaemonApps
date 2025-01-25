using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SolarBackup.States;

public class BackingUpDataState : SolarBackupState
{
    internal override Task Enter(ILogger logger, IScheduler scheduler, SolarBackup context)
    {
        logger.LogInformation("Solar backup: Backing up data stores through SSH / proxmox backup client.");
        return context.TransitionTo(logger, new VerifyingBackupsState());
    }

    internal override Task CheckProgress(ILogger logger, IScheduler scheduler, SolarBackup context)
    {
        //Not sure how we will track progress here
        return Task.CompletedTask;
    }
}