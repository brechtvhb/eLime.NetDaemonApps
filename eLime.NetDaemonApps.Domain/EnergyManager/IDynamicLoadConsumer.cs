namespace eLime.NetDaemonApps.Domain.EnergyManager;

internal interface IDynamicLoadConsumer
{
    public String Name { get; }
    public Int32 MinimumCurrent { get; }
    public Int32 MaximumCurrent { get; }
    public TimeSpan MinimumRebalancingInterval { get; }
    public BalancingMethod BalancingMethod { get; }
    public string BalanceOnBehalfOf { get; }
    public AllowBatteryPower AllowBatteryPower { get; }
    public double ReleasablePowerWhenBalancingOnBehalfOf { get; }

    public IDisposable? BalancingMethodChangedCommandHandler { get; set; }
    public IDisposable? BalanceOnBehalfOfChangedCommandHandler { get; set; }
    public IDisposable? AllowBatteryPowerChangedCommandHandler { get; set; }

    public void SetBalancingMethod(DateTimeOffset now, BalancingMethod balancingMethod);
    public void SetBalanceOnBehalfOf(string consumerGorup);
    public void SetAllowBatteryPower(AllowBatteryPower allowBatteryPower);
    public (Double current, Double netPowerChange) Rebalance(IGridMonitor gridMonitor, double totalNetChange);

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