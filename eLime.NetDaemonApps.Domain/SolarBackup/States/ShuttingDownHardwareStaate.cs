using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SolarBackup.States;

public class ShuttingDownHardwareState : SolarBackupState
{
    internal override Task Enter(ILogger logger, IScheduler scheduler, SolarBackup context)
    {
        //Use home assistant to shutdown synology
        return Task.CompletedTask;
    }

    internal override Task CheckProgress(ILogger logger, IScheduler scheduler, SolarBackup context)
    {
        //Safe to assume shutdown button always works?
        return context.TransitionTo(logger, new IdleState());
    }
}