using eLime.NetDaemonApps.Domain.EnergyManager.Grid;

namespace eLime.NetDaemonApps.Domain.EnergyManager.Consumers.DynamicConsumers;

internal interface IDynamicLoadConsumer
{
    internal string Name { get; }
    internal string BalanceOnBehalfOf { get; }
    internal AllowBatteryPower AllowBatteryPower { get; }
    internal double ReleasablePowerWhenBalancingOnBehalfOf { get; }

    internal (double current, double netPowerChange) Rebalance(IGridMonitor gridMonitor, Dictionary<LoadTimeFrames, double> consumerAverageLoadCorrections, double expectedLoadCorrections, double dynamicLoadAdjustments, double dynamicLoadThatCanBeScaledDownOnBehalfOf, double maximumDischargePower);

    internal bool ApplyExpectedLoadCorrections { get; }
    public static string CONSUMER_GROUP_SELF = "Self";
    public static string CONSUMER_GROUP_ALL = "All consumers";
}