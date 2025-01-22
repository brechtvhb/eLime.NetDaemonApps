using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SolarBackup.States;

public class ShuttingDownBackupServerState : SolarBackupState
{
    internal override void Enter(ILogger logger, IScheduler scheduler, SolarBackup context)
    {
        //API call to shut down PBS
    }

    internal override void CheckProgress(ILogger logger, IScheduler scheduler, SolarBackup context)
    {
        //Check status of PBS, wait one loop, then progress
        context.TransitionTo(logger, new ShuttingDownHardwareState());
    }
}