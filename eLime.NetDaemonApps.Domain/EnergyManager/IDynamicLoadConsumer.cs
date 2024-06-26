namespace eLime.NetDaemonApps.Domain.EnergyManager;

internal interface IDynamicLoadConsumer
{
    public String Name { get; }
    public Int32 MinimumCurrent { get; }
    public Int32 MaximumCurrent { get; }
    public TimeSpan MinimumRebalancingInterval { get; }
    public BalancingMethod BalancingMethod { get; }

    public IDisposable? BalancingMethodChangedCommandHandler { get; set; }

    public void SetBalancingMethod(DateTimeOffset now, BalancingMethod balancingMethod);
    public (Double current, Double netPowerChange) Rebalance(double netGridUsage, double peakUsage);
}

public enum BalancingMethod
{
    SolarOnly,
    SolarPreferred,
    NearPeak
}