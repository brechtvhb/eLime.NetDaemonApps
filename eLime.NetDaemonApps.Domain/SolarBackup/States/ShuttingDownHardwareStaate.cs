using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SolarBackup.States;

public class ShuttingDownHardwareState : SolarBackupState
{
    private DateTimeOffset _shutDownStartedAt;

    internal override Task Enter(ILogger logger, IScheduler scheduler, SolarBackup context)
    {
        logger.LogInformation("Solar backup: Shut down hardware");
        context.ShutDownServer();
        _shutDownStartedAt = scheduler.Now;
        return Task.CompletedTask;
    }

    internal override Task CheckProgress(ILogger logger, IScheduler scheduler, SolarBackup context)
    {
        if (_shutDownStartedAt.AddMinutes(2) >= scheduler.Now)
            return Task.CompletedTask;

        context.FinishBackup();
        return context.TransitionTo(logger, new IdleState());

    }
}