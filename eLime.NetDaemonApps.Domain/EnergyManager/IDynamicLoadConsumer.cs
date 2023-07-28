namespace eLime.NetDaemonApps.Domain.EnergyManager;

internal interface IDynamicLoadConsumer
{
    public String Name { get; }
    public Int32 MinimumCurrent { get; }
    public Int32 MaximumCurrent { get; }

    public (Double current, Double netPowerChange) Rebalance(Double netGridUsage);

}