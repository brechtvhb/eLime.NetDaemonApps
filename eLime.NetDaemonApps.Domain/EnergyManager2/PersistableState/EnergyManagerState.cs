
using eLime.NetDaemonApps.Domain.EnergyManager;

namespace eLime.NetDaemonApps.Domain.EnergyManager2.PersistableState;

#pragma warning disable CS8618, CS9264
internal class EnergyManagerState
{
    public EnergyConsumerState State { get; set; }
}