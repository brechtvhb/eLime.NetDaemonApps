using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.DeviceTracker;
using eLime.NetDaemonApps.Domain.Entities.Input;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.EnergyManager;

public class Car
{
    public String Name { get; }
    public CarChargingMode Mode { get; }

    public int? MinimumCurrent { get; }
    public int? MaximumCurrent { get; }
    public InputNumberEntity? CurrentEntity { get; set; }

    public Double BatteryCapacity { get; }
    public Boolean IgnoreStateOnForceCharge { get; set; }

    public NumericEntity BatteryPercentageSensor { get; }
    public NumericEntity? MaxBatteryPercentageSensor { get; }
    public BinarySensor CableConnectedSensor { get; }
    public DeviceTracker Location { get; }

    public Car(string name, CarChargingMode mode, InputNumberEntity? currentEntity, int? minimumCurrent, int? maximumCurrent, bool ignoreStateOnForceCharge,
        double batteryCapacity, NumericEntity batteryPercentageSensor, NumericEntity? maxBatteryPercentageSensor, BinarySensor cableConnectedSensor, DeviceTracker location)
    {
        Name = name;
        Mode = mode;

        CurrentEntity = currentEntity;
        MinimumCurrent = minimumCurrent;
        MaximumCurrent = maximumCurrent;

        BatteryCapacity = batteryCapacity;
        IgnoreStateOnForceCharge = ignoreStateOnForceCharge;
        BatteryPercentageSensor = batteryPercentageSensor;
        MaxBatteryPercentageSensor = maxBatteryPercentageSensor;
        CableConnectedSensor = cableConnectedSensor;
        Location = location;
    }

    public Boolean IsConnectedToHomeCharger => CableConnectedSensor.IsOn() && Location.State == "home";

    public Boolean NeedsEnergy => MaxBatteryPercentageSensor != null
        ? BatteryPercentageSensor.State < MaxBatteryPercentageSensor.State
        : BatteryPercentageSensor.State < 100;

}

public enum CarChargingMode
{
    Ac1Phase,
    Ac3Phase
}