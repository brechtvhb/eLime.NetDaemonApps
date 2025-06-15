namespace eLime.NetDaemonApps.Domain.EnergyManager2.Consumers;

internal interface IDynamicLoadConsumer2
{
    internal string Name { get; }
    internal int MinimumCurrent { get; }
    internal int MaximumCurrent { get; }
    internal TimeSpan MinimumRebalancingInterval { get; }
    internal BalancingMethod BalancingMethod { get; }
    internal string BalanceOnBehalfOf { get; }
    internal AllowBatteryPower AllowBatteryPower { get; }
    internal double ReleasablePowerWhenBalancingOnBehalfOf { get; }

    internal (double current, double netPowerChange) Rebalance(IGridMonitor2 gridMonitor, double totalNetChange);

    public static string CONSUMER_GROUP_SELF = "Self";
    public static string CONSUMER_GROUP_ALL = "All consumers";
}

public enum BalancingMethod
{
    SolarSurplus,
    SolarOnly,
    MidPoint,
    SolarPreferred,
    MidPeak,
    NearPeak,
    MaximizeQuarterPeak
}

public enum AllowBatteryPower
{
    Yes,
    No
}