namespace eLime.NetDaemonApps.Config.EnergyManager;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

public class CarChargerEnergyConsumerConfig
{
    public int MinimumCurrent { get; set; }
    public int MaximumCurrent { get; set; }
    public int OffCurrent { get; set; }

    public string CurrentEntity { get; set; }
    public string VoltageEntity { get; set; }
    public string StateSensor { get; set; }
    public List<CarConfig> Cars { get; set; } = [];

}

public class CarConfig
{
    public string Name { get; set; }
    public CarChargingMode Mode { get; set; }

    public string? ChargerSwitch { get; set; }
    public string? CurrentEntity { get; set; }

    public int? MinimumCurrent { get; set; }
    public int? MaximumCurrent { get; set; }

    public double BatteryCapacity { get; set; }
    public string BatteryPercentageSensor { get; set; }
    public string? MaxBatteryPercentageSensor { get; set; }
    public bool RemainOnAtFullBattery { get; set; }

    public string CableConnectedSensor { get; set; }
    public bool AutoPowerOnWhenConnecting { get; set; }
    public string Location { get; set; }
}


public enum CarChargingMode
{
    Ac1Phase,
    Ac3Phase
}