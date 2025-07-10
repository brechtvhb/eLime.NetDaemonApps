namespace eLime.NetDaemonApps.Domain.EnergyManager.BatteryManager.Batteries;

#pragma warning disable CS8618, CS9264
internal class BatteryState
{
    public DateTimeOffset? LastChange { get; set; }

    public List<RoundTripEfficiencyReferencePoint> RoundTripEfficiencyReferencePoints { get; set; } = [];
    public double RoundTripEfficiency { get; set; }

}

internal class RoundTripEfficiencyReferencePoint
{
    public int ReferencePoint { get; set; }
    public double LastTotalEnergyCharged { get; set; }
    public double LastTotalEnergyDischarged { get; set; }

    public static RoundTripEfficiencyReferencePoint Create(int referencePoint, double lastTotalEnergyCharged, double lastTotalEnergyDischarged)
    {
        return new RoundTripEfficiencyReferencePoint
        {
            ReferencePoint = referencePoint,
            LastTotalEnergyCharged = lastTotalEnergyCharged,
            LastTotalEnergyDischarged = lastTotalEnergyDischarged,
        };
    }

    public void Update(double lastTotalEnergyCharged, double lastTotalEnergyDischarged)
    {
        LastTotalEnergyCharged = lastTotalEnergyCharged;
        LastTotalEnergyDischarged = lastTotalEnergyDischarged;
    }
}