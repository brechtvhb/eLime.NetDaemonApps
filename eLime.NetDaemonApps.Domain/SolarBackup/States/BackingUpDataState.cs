using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SolarBackup.States;

public class BackingUpDataState : SolarBackupState
{
    internal override void Enter(ILogger logger, IScheduler scheduler, SolarBackup context)
    {
        //not yet implemented
        context.TransitionTo(logger, new VerifyingBackupsState());
        //SSH stuff to call proxmox backup client script
    }

    internal override void CheckProgress(ILogger logger, IScheduler scheduler, SolarBackup context)
    {
        //Not sure how we will track progress here

    }
}