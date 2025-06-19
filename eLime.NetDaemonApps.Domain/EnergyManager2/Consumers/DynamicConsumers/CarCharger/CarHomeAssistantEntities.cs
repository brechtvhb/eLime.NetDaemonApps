using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.DeviceTracker;
using eLime.NetDaemonApps.Domain.Entities.Input;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;

namespace eLime.NetDaemonApps.Domain.EnergyManager2.Consumers.DynamicConsumers.CarCharger;

public class CarHomeAssistantEntities(CarConfiguration config) : IDisposable
{
    public BinarySwitch? ChargerSwitch = config.ChargerSwitch;
    public InputNumberEntity? CurrentNumber = config.CurrentNumber;
    public NumericSensor BatteryPercentageSensor = config.BatteryPercentageSensor;
    public NumericSensor? MaxBatteryPercentageSensor = config.MaxBatteryPercentageSensor;
    public BinarySensor CableConnectedSensor = config.CableConnectedSensor;
    public DeviceTracker Location = config.Location;

    public void Dispose()
    {
        ChargerSwitch?.Dispose();
        BatteryPercentageSensor.Dispose();
        MaxBatteryPercentageSensor?.Dispose();
        CableConnectedSensor.Dispose();
    }
}