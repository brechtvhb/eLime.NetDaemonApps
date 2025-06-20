using eLime.NetDaemonApps.Domain.EnergyManager.Consumers;

namespace eLime.NetDaemonApps.Domain.EnergyManager;

#pragma warning disable CS8618, CS9264
internal class EnergyManagerState
{
    public EnergyConsumerState State { get; set; }
    public List<string>? NeedEnergyConsumers { get; set; }
    public List<string>? CriticalNeedEnergyConsumers { get; set; }
    public List<string>? RunningConsumers { get; set; }
    public DateTimeOffset LastChange { get; set; }
}