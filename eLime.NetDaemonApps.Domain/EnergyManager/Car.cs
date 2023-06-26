using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.EnergyManager;

public class Car
{
    public Double BatteryCapacity { get; set; }
    public NumericEntity BatteryPercentageSensor { get; set; }
    public BinarySensor CableConnectedSensor { get; set; }
}