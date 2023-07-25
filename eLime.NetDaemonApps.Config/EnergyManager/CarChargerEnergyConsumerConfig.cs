﻿namespace eLime.NetDaemonApps.Config.EnergyManager;

public class CarChargerEnergyConsumerConfig
{
    public Int32 MinimumCurrent { get; set; }
    public Int32 MaximumCurrent { get; set; }
    public Int32 OffCurrent { get; set; }

    public String CurrentEntity { get; set; }
    public String StateSensor { get; set; }
    public List<CarConfig> Cars { get; set; }

}

public class CarConfig
{
    public String Name { get; set; }
    public Double BatteryCapacity { get; set; }
    public String BatteryPercentageSensor { get; set; }
    public String CableConnectedSensor { get; set; }
}