namespace eLime.NetDaemonApps.Config.EnergyManager;

#pragma warning disable CS8618
public class BatteryConfig
{
    public string Name { get; set; }
    public decimal Capacity { get; set; } //in kWh
    public int MinimumStateOfCharge { get; set; }
    public int MaxChargePower { get; set; }
    public int MaxDischargePower { get; set; }
    public string PowerSensor { get; set; }
    public string StateOfChargeSensor { get; set; }
    public string TotalEnergyChargedSensor { get; set; }
    public string TotalEnergyDischargedSensor { get; set; }

    public string MaxChargePowerEntity { get; set; }
    public string MaxDischargePowerEntity { get; set; }

    //to generate: OperatingMode (Automatic, Manual), ReservedPeakShavingStateOfCharge, RoundTripEfficiency

}