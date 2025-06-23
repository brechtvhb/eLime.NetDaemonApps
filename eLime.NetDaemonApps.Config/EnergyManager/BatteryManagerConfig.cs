namespace eLime.NetDaemonApps.Config.EnergyManager;

#pragma warning disable CS8618
public class BatteryManagerConfig
{
    public string TotalChargePowerSensor { get; set; }
    public string TotalDischargePowerSensor { get; set; }

    public List<BatteryConfig> Batteries { get; set; }

}