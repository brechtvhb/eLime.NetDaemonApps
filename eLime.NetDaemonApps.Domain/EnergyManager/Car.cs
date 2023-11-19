using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.EnergyManager;

public class Car
{
    public String Name { get; }
    public Double BatteryCapacity { get; }
    public Boolean IgnoreStateOnForceCharge { get; set; }

    public NumericEntity BatteryPercentageSensor { get; }
    public BinarySensor CableConnectedSensor { get; }

    public Car(string name, double batteryCapacity, bool ignoreStateOnForceCharge, NumericEntity batteryPercentageSensor, BinarySensor cableConnectedSensor)
    {
        Name = name;
        BatteryCapacity = batteryCapacity;
        IgnoreStateOnForceCharge = ignoreStateOnForceCharge;
        BatteryPercentageSensor = batteryPercentageSensor;
        CableConnectedSensor = cableConnectedSensor;
    }
}