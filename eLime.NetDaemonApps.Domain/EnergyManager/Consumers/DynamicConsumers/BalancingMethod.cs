namespace eLime.NetDaemonApps.Domain.EnergyManager.Consumers.DynamicConsumers;

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