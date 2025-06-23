namespace eLime.NetDaemonApps.Config.EnergyManager;

#pragma warning disable CS8618
public class BatteryManagerConfig
{
    public string TotalChargePowerSensor { get; set; }
    public string TotalDischargePowerSensor { get; set; }

    //to generate: TotalCapacity, RemainingCapacity, AggregatedStateOfCharge

    public List<BatteryConfig> Batteries { get; set; }

}