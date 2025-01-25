using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SolarBackup.States;

public class ShuttingDownBackupServerState : SolarBackupState
{
    private DateTimeOffset _offlineSince = DateTimeOffset.MaxValue;
    internal override Task Enter(ILogger logger, IScheduler scheduler, SolarBackup context)
    {
        logger.LogInformation("Solar backup: Shut down PBS.");
        return context.PbsClient.Shutdown();
    }

    internal override async Task CheckProgress(ILogger logger, IScheduler scheduler, SolarBackup context)
    {
        if (_offlineSince > scheduler.Now)
        {
            var isOnline = await context.PbsClient.IsOnline();

            if (!isOnline)
                _offlineSince = scheduler.Now.UtcDateTime;
        }

        if (_offlineSince.AddMinutes(1) < scheduler.Now)
        {
            logger.LogInformation("Solar backup: No HTTP response since one minute, shutting down.");
            await context.TransitionTo(logger, new ShuttingDownHardwareState());
        }
    }
}