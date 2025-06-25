namespace eLime.NetDaemonApps.Domain.EnergyManager.BatteryManager.Batteries;

#pragma warning disable CS8618, CS9264
internal class BatteryState
{
    public DateTimeOffset? LastChange { get; set; }
    public double LastTotalEnergyChargedAtRteReferencePoint { get; set; }
    public double LastTotalEnergyDischargedAtRteReferencePoint { get; set; }
    public int LastRteStateOfChargeReferencePoint { get; set; }
    public double RoundTripEfficiency { get; set; }
}