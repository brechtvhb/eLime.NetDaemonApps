using AllowBatteryPower = eLime.NetDaemonApps.Domain.EnergyManager.Consumers.DynamicConsumers.AllowBatteryPower;
using BalancingMethod = eLime.NetDaemonApps.Domain.EnergyManager.Consumers.DynamicConsumers.BalancingMethod;

namespace eLime.NetDaemonApps.Domain.EnergyManager.Consumers;

#pragma warning disable CS8618, CS9264
internal class ConsumerState
{
    public EnergyConsumerState State { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? LastRun { get; set; }
    public BalancingMethod? BalancingMethod { get; set; }
    public string? BalanceOnBehalfOf { get; set; }
    public AllowBatteryPower? AllowBatteryPower { get; set; }
}