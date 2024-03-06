using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.EnergyManager;

public class Car
{
    public String Name { get; }
    public bool Supports3Phase { get; }
    public Double BatteryCapacity { get; }
    public Boolean IgnoreStateOnForceCharge { get; set; }

    public NumericEntity BatteryPercentageSensor { get; }
    public NumericEntity? MaxBatteryPercentageSensor { get; }
    public BinarySensor CableConnectedSensor { get; }

    public Car(string name, bool supports3Phase, double batteryCapacity, bool ignoreStateOnForceCharge, NumericEntity batteryPercentageSensor, NumericEntity? maxBatteryPercentageSensor, BinarySensor cableConnectedSensor)
    {
        Name = name;
        Supports3Phase = supports3Phase;
        BatteryCapacity = batteryCapacity;
        IgnoreStateOnForceCharge = ignoreStateOnForceCharge;
        BatteryPercentageSensor = batteryPercentageSensor;
        MaxBatteryPercentageSensor = maxBatteryPercentageSensor;
        CableConnectedSensor = cableConnectedSensor;
    }
}