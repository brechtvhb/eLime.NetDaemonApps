using eLime.NetDaemonApps.Config.EnergyManager;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.DeviceTracker;
using eLime.NetDaemonApps.Domain.Entities.Input;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.Helper;
using NetDaemon.HassModel;
#pragma warning disable CS8601 // Possible null reference assignment.

namespace eLime.NetDaemonApps.Domain.EnergyManager2.Configuration;

public class CarConfiguration
{
    public CarConfiguration(IHaContext haContext, CarConfig config)
    {
        Name = config.Name;
        Mode = Enum<CarChargingMode>.Cast(config.Mode);
        ChargerSwitch = !string.IsNullOrWhiteSpace(config.ChargerSwitch) ? BinarySwitch.Create(haContext, config.ChargerSwitch) : null;
        CurrentNumber = !string.IsNullOrWhiteSpace(config.CurrentEntity) ? new InputNumberEntity(haContext, config.CurrentEntity) : null;
        MinimumCurrent = config.MinimumCurrent;
        MaximumCurrent = config.MaximumCurrent;
        BatteryCapacity = config.BatteryCapacity;
        BatteryPercentageSensor = new NumericSensor(haContext, config.BatteryPercentageSensor);
        MaxBatteryPercentageSensor = !string.IsNullOrWhiteSpace(config.MaxBatteryPercentageSensor) ? new NumericSensor(haContext, config.MaxBatteryPercentageSensor) : null;
        RemainOnAtFullBattery = config.RemainOnAtFullBattery;
        CableConnectedSensor = BinarySensor.Create(haContext, config.CableConnectedSensor);
        AutoPowerOnWhenConnecting = config.AutoPowerOnWhenConnecting;
        Location = new DeviceTracker(haContext, config.Location);
    }
    public string Name { get; set; }
    public CarChargingMode Mode { get; set; }
    public BinarySwitch? ChargerSwitch { get; set; }
    public InputNumberEntity? CurrentNumber { get; set; }
    public int? MinimumCurrent { get; set; }
    public int? MaximumCurrent { get; set; }
    public double BatteryCapacity { get; set; }
    public NumericSensor BatteryPercentageSensor { get; set; }
    public NumericSensor? MaxBatteryPercentageSensor { get; set; }
    public bool RemainOnAtFullBattery { get; set; }
    public BinarySensor CableConnectedSensor { get; set; }
    public bool AutoPowerOnWhenConnecting { get; set; }
    public DeviceTracker Location { get; set; }
}