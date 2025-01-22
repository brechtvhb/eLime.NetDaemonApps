using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SolarBackup.States;

public class GarbageCollectingState : SolarBackupState
{
    internal override void Enter(ILogger logger, IScheduler scheduler, SolarBackup context)
    {
        //API call to garbage collect data store
    }

    internal override void CheckProgress(ILogger logger, IScheduler scheduler, SolarBackup context)
    {
        //check if garbage collection task completed(through API call)
        context.TransitionTo(logger, new ShuttingDownBackupServerState());
    }
}