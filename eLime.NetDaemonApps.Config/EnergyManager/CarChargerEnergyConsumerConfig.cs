﻿namespace eLime.NetDaemonApps.Config.EnergyManager;

public class CarChargerEnergyConsumerConfig
{
    public Int32 MinimumCurrent { get; set; }
    public Int32 MaximumCurrent { get; set; }
    public Int32 OffCurrent { get; set; }

    public String CurrentEntity { get; set; }
    public String VoltageEntity { get; set; }
    public String StateSensor { get; set; }
    public List<CarConfig> Cars { get; set; }

}

public class CarConfig
{
    public String Name { get; set; }
    public CarChargingMode Mode { get; set; }

    public string ChargerSwitch { get; set; }
    public String CurrentEntity { get; set; }

    public String ChargingStateSensor { get; set; }
    public int? MinimumCurrent { get; set; }
    public int? MaximumCurrent { get; set; }

    public Double BatteryCapacity { get; set; }
    public String BatteryPercentageSensor { get; set; }
    public String MaxBatteryPercentageSensor { get; set; }
    public Boolean RemainOnAtFullBattery { get; set; }

    public String CableConnectedSensor { get; set; }
    public Boolean AutoPowerOnWhenConnecting { get; set; }
    public String Location { get; set; }
}


public enum CarChargingMode
{
    Ac1Phase,
    Ac3Phase
}