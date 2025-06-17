
using eLime.NetDaemonApps.Domain.EnergyManager;
using AllowBatteryPower = eLime.NetDaemonApps.Domain.EnergyManager2.Consumers.AllowBatteryPower;
using BalancingMethod = eLime.NetDaemonApps.Domain.EnergyManager2.Consumers.BalancingMethod;

namespace eLime.NetDaemonApps.Domain.EnergyManager2.PersistableState;

#pragma warning disable CS8618, CS9264
internal class ConsumerState //Should be energy consumer state, but that is already used as enum, rename enum to EnergyConsumerStatus first
{
    public EnergyConsumerState State
    {
        get;
        set;
    }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? LastRun { get; set; }
    public BalancingMethod? BalancingMethod { get; set; }
    public string? BalanceOnBehalfOf { get; set; }
    public AllowBatteryPower? AllowBatteryPower { get; set; }
}