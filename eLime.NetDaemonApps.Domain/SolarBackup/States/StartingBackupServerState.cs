using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SolarBackup.States;

public class StartingBackupServerState : SolarBackupState
{
    internal override Task Enter(ILogger logger, IScheduler scheduler, SolarBackup context)
    {
        logger.LogInformation("Solar backup: Starting server");
        context.BootServer();
        return Task.CompletedTask;
    }

    internal override async Task CheckProgress(ILogger logger, IScheduler scheduler, SolarBackup context)
    {
        //Check if PBS storage came online (API call)
        var isOnline = await context.PveClient.CheckPbsStorageStatus();

        if (isOnline)
        {
            await context.TransitionTo(logger, new BackingUpWorkloadState());
            return;
        }

        if (context.StartedAt?.AddMinutes(5) < scheduler.Now)
        {
            logger.LogInformation("Solar backup: Taking too long before server is online, trying wake on lan again");
            context.BootServer();
        }
    }
}