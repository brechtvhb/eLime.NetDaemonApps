using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SolarBackup.States;

public class IdleState : SolarBackupState
{
    internal override void Enter(ILogger logger, IScheduler scheduler, SolarBackup context)
    {
        context.ClearStartedAt();
    }

    internal override void CheckProgress(ILogger logger, IScheduler scheduler, SolarBackup context)
    {
        return;
    }
}