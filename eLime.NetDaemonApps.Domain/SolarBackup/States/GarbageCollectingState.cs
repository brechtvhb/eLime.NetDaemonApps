using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SolarBackup.States;

public class GarbageCollectingState : SolarBackupState
{
    private string? _taskId;

    internal override async Task Enter(ILogger logger, IScheduler scheduler, SolarBackup context)
    {
        logger.LogInformation("Solar backup: Starting garbage collection task.");
        _taskId = await context.PbsClient.StartGarbageCollectTask();
    }

    internal override async Task CheckProgress(ILogger logger, IScheduler scheduler, SolarBackup context)
    {
        if (_taskId == null)
        {
            logger.LogInformation("Solar backup: No garbage collection task was found. Starting garbage collection task.");
            _taskId = await context.PbsClient.StartGarbageCollectTask();
            return;
        }

        //check if garbage collection task completed(through API call)
        logger.LogTrace("Solar backup: Checking if garbage collection was completed.");
        var taskCompleted = await context.PbsClient.CheckIfTaskCompleted(_taskId);
        if (taskCompleted)
            await context.TransitionTo(logger, new ShuttingDownBackupServerState());
    }
}