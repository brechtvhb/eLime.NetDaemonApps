using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SolarBackup.States;

public class VerifyingBackupsState : SolarBackupState
{
    private string? _taskId;

    internal override async Task Enter(ILogger logger, IScheduler scheduler, SolarBackup context)
    {
        logger.LogInformation("Solar backup: Starting verify task.");
        _taskId = await context.PbsClient.StartVerifyTask();
    }

    internal override async Task CheckProgress(ILogger logger, IScheduler scheduler, SolarBackup context)
    {
        if (_taskId == null)
        {
            logger.LogInformation("Solar backup: No verify task ID was found. Starting verify task.");
            _taskId = await context.PbsClient.StartVerifyTask();
            return;
        }

        //check if verify task completed (through API call)
        logger.LogTrace("Solar backup: Checking if verify task was completed.");
        var taskCompleted = await context.PbsClient.CheckIfTaskCompleted(_taskId);
        if (taskCompleted)
            await context.TransitionTo(logger, new PruningBackupsState());
    }
}