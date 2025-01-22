using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SolarBackup.States;

public class BackingUpWorkloadState : SolarBackupState
{
    internal override void Enter(ILogger logger, IScheduler scheduler, SolarBackup context)
    {
        //API call to trigger backup for all VM & LXC workloads
    }

    internal override void CheckProgress(ILogger logger, IScheduler scheduler, SolarBackup context)
    {
        //Check if backup task is completed (through API call)
        context.TransitionTo(logger, new BackingUpDataState());
    }
}