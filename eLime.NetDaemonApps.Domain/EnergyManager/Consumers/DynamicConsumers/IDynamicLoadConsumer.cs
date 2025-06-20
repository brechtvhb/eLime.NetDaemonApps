using eLime.NetDaemonApps.Domain.EnergyManager.Grid;

namespace eLime.NetDaemonApps.Domain.EnergyManager.Consumers.DynamicConsumers;

internal interface IDynamicLoadConsumer
{
    internal string Name { get; }
    internal int MinimumCurrent { get; }
    internal int MaximumCurrent { get; }
    internal TimeSpan MinimumRebalancingInterval { get; }
    internal BalancingMethod BalancingMethod { get; }
    internal string BalanceOnBehalfOf { get; }
    internal AllowBatteryPower AllowBatteryPower { get; }
    internal double ReleasablePowerWhenBalancingOnBehalfOf { get; }

    internal (double current, double netPowerChange) Rebalance(IGridMonitor gridMonitor, double totalNetChange);

    public static string CONSUMER_GROUP_SELF = "Self";
    public static string CONSUMER_GROUP_ALL = "All consumers";
}