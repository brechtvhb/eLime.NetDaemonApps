using eLime.NetDaemonApps.Config.EnergyManager;
using CarChargingMode = eLime.NetDaemonApps.Config.EnergyManager.CarChargingMode;

#pragma warning disable CS8618, CS9264

namespace eLime.NetDaemonApps.Tests.EnergyManager.Builders;

public class CarChargerEnergyConsumerBuilder
{
    private string _name;
    private List<string> _consumerGroups = [];

    private string _powerEntity;
    private string? _criticallyNeeded;
    private double _switchOnLoad;
    private double _switchOffLoad;

    private TimeSpan? _minimumRuntime;
    private TimeSpan? _maximumRuntime;
    private TimeSpan? _minimumTimeout;
    private TimeSpan? _maximumTimeout;
    private readonly List<TimeWindowConfig> _timeWindows = [];

    public int _minimumCurrent;
    public int _maximumCurrent;
    public int _offCurrent;
    public string _currentEntity;
    public string _voltageEntity;
    public string _stateSensor;

    public List<CarConfig> _cars = [];

    public static CarChargerEnergyConsumerBuilder Passat()
    {
        return new CarChargerEnergyConsumerBuilder()
            .WithName("Veton")
            .WithStateSensor("sensor.veton_state")
            .WithLoads(-800, 1000)
            .WithCurrents("input_number.veton_current", 6, 16, 5)
            .WithVoltage("sensor.veton_voltage")
            .WithPowerSensor("sensor.veton_power")
            .AddCar("Passat GTE", CarChargingMode.Ac1Phase, null, null, 6, 16, null, 11.5, "sensor.passat_battery", null, false, "binary_sensor.passat_cable_connected", "device_tracker.passat_position");
    }

    public static CarChargerEnergyConsumerBuilder Tesla(bool remainOnAtFullBattery = false)
    {
        return new CarChargerEnergyConsumerBuilder()
            .WithName("Veton")
            .WithStateSensor("sensor.veton_state")
            .WithLoads(-800, 1000)
            .WithCurrents("input_number.veton_current", 6, 16, 5)
            .WithVoltage("sensor.veton_voltage")
            .WithPowerSensor("sensor.veton_power")
            .AddCar("MY2024", CarChargingMode.Ac3Phase, "switch.my2024_charge", "input_number.my2024", 1, 16, "sensor.my2024_charging", 75, "sensor.my2024_battery", "sensor.my2024_battery_max_charge", remainOnAtFullBattery, "binary_sensor.my2024_cable_connected", "device_tracker.my2024_position");
    }

    public CarChargerEnergyConsumerBuilder WithName(string name)
    {
        _name = name;

        return this;
    }

    public CarChargerEnergyConsumerBuilder AddConsumerGroup(string consumerGroup)
    {
        _consumerGroups.Add(consumerGroup);
        return this;
    }

    public CarChargerEnergyConsumerBuilder WithCriticalSensor(string sensorName)
    {
        _criticallyNeeded = sensorName;

        return this;
    }

    public CarChargerEnergyConsumerBuilder WithRuntime(TimeSpan? minimum, TimeSpan? maximum)
    {
        _minimumRuntime = minimum;
        _maximumRuntime = maximum;

        return this;
    }

    public CarChargerEnergyConsumerBuilder WithTimeout(TimeSpan? minimum, TimeSpan? maximum)
    {
        _minimumTimeout = minimum;
        _maximumTimeout = maximum;

        return this;
    }

    public CarChargerEnergyConsumerBuilder AddTimeWindow(string? isActive, TimeSpan start, TimeSpan end)
    {
        _timeWindows.Add(new TimeWindowConfig
        {
            ActiveSensor = isActive,
            Start = start,
            End = end
        });
        return this;
    }

    public CarChargerEnergyConsumerBuilder WithLoads(double switchOn, double switchOff)
    {
        _switchOnLoad = switchOn;
        _switchOffLoad = switchOff;
        return this;
    }

    public CarChargerEnergyConsumerBuilder WithStateSensor(string stateSensor)
    {
        _stateSensor = stateSensor;
        return this;
    }

    public CarChargerEnergyConsumerBuilder WithPowerSensor(string powerSensor)
    {
        _powerEntity = powerSensor;
        return this;
    }
    public CarChargerEnergyConsumerBuilder WithCurrents(string currentEntity, int minimumCurrent, int maximumCurrent, int offCurrent)
    {
        _currentEntity = currentEntity;
        _minimumCurrent = minimumCurrent;
        _maximumCurrent = maximumCurrent;
        _offCurrent = offCurrent;
        return this;
    }
    public CarChargerEnergyConsumerBuilder WithVoltage(string voltageEntity)
    {
        _voltageEntity = voltageEntity;
        return this;
    }

    public CarChargerEnergyConsumerBuilder AddCar(string name, CarChargingMode mode, string? chargerSwitch, string? currentEntity, int? minimumCurrent, int? maximumCurrent, string? carChargingState, double batteryCapacity, string batteryPercentageSensor, string? maxBatteryPercentageSensor, bool remainOnAtFullBattery, string cableConnectedSensor, string location)
    {
        _cars.Add(new CarConfig
        {
            Name = name,
            Mode = mode,
            ChargerSwitch = chargerSwitch,
            CurrentEntity = currentEntity,
            MinimumCurrent = minimumCurrent,
            MaximumCurrent = maximumCurrent,
            BatteryCapacity = batteryCapacity,
            BatteryPercentageSensor = batteryPercentageSensor,
            MaxBatteryPercentageSensor = maxBatteryPercentageSensor,
            RemainOnAtFullBattery = remainOnAtFullBattery,
            CableConnectedSensor = cableConnectedSensor,
            Location = location
        });
        return this;
    }

    public EnergyConsumerConfig Build()
    {
        return new EnergyConsumerConfig
        {
            Name = _name,
            ConsumerGroups = _consumerGroups,
            PowerUsageEntity = _powerEntity,
            CriticallyNeededEntity = _criticallyNeeded,
            SwitchOnLoad = _switchOnLoad,
            SwitchOffLoad = _switchOffLoad,
            MinimumRuntime = _minimumRuntime,
            MaximumRuntime = _maximumRuntime,
            MinimumTimeout = _minimumTimeout,
            MaximumTimeout = _maximumTimeout,
            TimeWindows = _timeWindows,
            CarCharger = new CarChargerEnergyConsumerConfig
            {
                StateSensor = _stateSensor,
                MinimumCurrent = _minimumCurrent,
                MaximumCurrent = _maximumCurrent,
                OffCurrent = _offCurrent,
                CurrentEntity = _currentEntity,
                VoltageEntity = _voltageEntity,
                Cars = _cars,
            },
        };
    }
}