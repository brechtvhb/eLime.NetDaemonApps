using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SolarBackup.States;

public class VerifyingBackupsState : SolarBackupState
{
    internal override void Enter(ILogger logger, IScheduler scheduler, SolarBackup context)
    {
        //API call to verify backups
    }

    internal override void CheckProgress(ILogger logger, IScheduler scheduler, SolarBackup context)
    {
        //check if verify task completed (through API call)
        context.TransitionTo(logger, new PruningBackupsState());
    }
}