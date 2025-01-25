using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SolarBackup.States;

public class PruningBackupsState : SolarBackupState
{
    private string? _taskId;

    internal override async Task Enter(ILogger logger, IScheduler scheduler, SolarBackup context)
    {
        logger.LogInformation("Solar backup: Starting prune task.");
        _taskId = await context.PbsClient.StartPruneTask();
    }

    internal override async Task CheckProgress(ILogger logger, IScheduler scheduler, SolarBackup context)
    {
        if (_taskId == null)
        {
            _taskId = await context.PbsClient.StartPruneTask();
            return;
        }

        //check if prune task completed (through API call)
        logger.LogInformation("Solar backup: Checking if prune task completed.");
        var taskCompleted = await context.PbsClient.CheckIfTaskCompleted(_taskId);
        if (taskCompleted)
            await context.TransitionTo(logger, new GarbageCollectingState());
    }
}