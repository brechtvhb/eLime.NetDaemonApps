using eLime.NetDaemonApps.Domain.EnergyManager2.Configuration;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.DeviceTracker;
using eLime.NetDaemonApps.Domain.Entities.Input;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.Entities.TextSensors;

namespace eLime.NetDaemonApps.Domain.EnergyManager2.HomeAssistant;

public class CarChargerEnergyConsumerHomeAssistantEntities(EnergyConsumerConfiguration config)
    : EnergyConsumerHomeAssistantEntities(config)
{
    internal InputNumberEntity CurrentNumber = config.CarCharger!.CurrentSensor;
    internal NumericSensor VoltageSensor = config.CarCharger!.VoltageSensor;
    internal TextSensor StateSensor = config.CarCharger!.StateSensor;
    public new void Dispose()
    {
        base.Dispose();
        CurrentNumber.Dispose();
        VoltageSensor.Dispose();
        StateSensor.Dispose();
    }
}

public class CarHomeAssistantEntities(CarConfiguration config) : IDisposable
{
    public BinarySwitch? ChargerSwitch = config.ChargerSwitch;
    public InputNumberEntity? CurrentNumber = config.CurrentNumber;
    public TextSensor? ChargingStateSensor = config.ChargingStateSensor;
    public NumericSensor BatteryPercentageSensor = config.BatteryPercentageSensor;
    public NumericSensor? MaxBatteryPercentageSensor = config.MaxBatteryPercentageSensor;
    public BinarySensor CableConnectedSensor = config.CableConnectedSensor;
    public DeviceTracker Location = config.Location;

    public void Dispose()
    {
        ChargerSwitch?.Dispose();
        ChargingStateSensor?.Dispose();
        BatteryPercentageSensor.Dispose();
        MaxBatteryPercentageSensor?.Dispose();
        CableConnectedSensor.Dispose();
    }
}