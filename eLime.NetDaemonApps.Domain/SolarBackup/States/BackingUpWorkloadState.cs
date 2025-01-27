using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SolarBackup.States;

public class BackingUpWorkloadState : SolarBackupState
{
    private string? _taskId;

    internal override async Task Enter(ILogger logger, IScheduler scheduler, SolarBackup context)
    {
        logger.LogInformation("Solar backup: Starting backup of VMs and LXC workloads.");
        _taskId = await context.PveClient.StartBackup();
    }

    internal override async Task CheckProgress(ILogger logger, IScheduler scheduler, SolarBackup context)
    {
        if (_taskId == null)
        {
            logger.LogInformation("Solar backup: Found no task ID for backup. Starting backup of VMs and LXC workloads again.");
            _taskId = await context.PveClient.StartBackup();
            return;
        }

        logger.LogTrace("Solar backup: Checking if backup was completed.");
        var backupCompleted = await context.PveClient.CheckIfTaskCompleted(_taskId);
        if (backupCompleted)
            await context.TransitionTo(logger, new BackingUpDataState());
    }
}