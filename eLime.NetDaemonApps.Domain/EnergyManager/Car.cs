using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.EnergyManager;

public class Car
{
    public String Name { get; }
    public Double BatteryCapacity { get; }
    public NumericEntity BatteryPercentageSensor { get; }
    public BinarySensor CableConnectedSensor { get; }

    public Car(string name, double batteryCapacity, NumericEntity batteryPercentageSensor, BinarySensor cableConnectedSensor)
    {
        Name = name;
        BatteryCapacity = batteryCapacity;
        BatteryPercentageSensor = batteryPercentageSensor;
        CableConnectedSensor = cableConnectedSensor;
    }
}