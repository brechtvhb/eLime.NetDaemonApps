using eLime.NetDaemonApps.Domain.EnergyManager;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.DeviceTracker;
using eLime.NetDaemonApps.Domain.Entities.Input;
using eLime.NetDaemonApps.Domain.Entities.TextSensors;
using eLime.NetDaemonApps.Tests.Helpers;
using Microsoft.Extensions.Logging;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Tests.Builders;

public class CarChargerEnergyConsumerBuilder
{
    private readonly ILogger _logger;
    private readonly AppTestContext _testCtx;
    private String _name;

    private NumericEntity _powerUsage;
    private BinarySensor? _criticallyNeeded;
    private Double _switchOnLoad;
    private Double _switchOffLoad;

    private TimeSpan? _minimumRuntime;
    private TimeSpan? _maximumRuntime;
    private TimeSpan? _minimumTimeout;
    private TimeSpan? _maximumTimeout;
    private List<TimeWindow> _timeWindows = new();

    public int _minimumCurrent;
    public int _maximumCurrent;
    public int _offCurrent;
    private BalancingMethod _balancingMethod = BalancingMethod.SolarOnly;

    public InputNumberEntity _currentEntity;
    public NumericEntity _voltageEntity;
    public TextSensor _stateSensor;

    public List<Car> _cars = new();

    public CarChargerEnergyConsumerBuilder(ILogger logger, AppTestContext testCtx)
    {
        _logger = logger;
        _testCtx = testCtx;

        _name = "Veton";
        _powerUsage = new NumericEntity(_testCtx.HaContext, "sensor.x");
        _switchOnLoad = -800;
        _switchOffLoad = 1000;

        _minimumCurrent = 6;
        _maximumCurrent = 16;
        _offCurrent = 5;

        _currentEntity = InputNumberEntity.Create(_testCtx.HaContext, "input_number.y");
        _voltageEntity = new NumericEntity(_testCtx.HaContext, "sensor.voltage");
        WithStateSensor(TextSensor.Create(_testCtx.HaContext, "sensor.z"));
        AddCar("Passat GTE", CarChargingMode.Ac1Phase, null, null, 6, 16, 11.5, new NumericEntity(_testCtx.HaContext, "sensor.a"), null, false, new BinarySensor(_testCtx.HaContext, "binary_sensor.b"), new DeviceTracker(_testCtx.HaContext, "device_tracker.passat_gte_position"));
    }

    public CarChargerEnergyConsumerBuilder InitTeslaTests(bool remainOffAtFullBattery = false)
    {
        _name = "Veton";
        _powerUsage = new NumericEntity(_testCtx.HaContext, "sensor.x");
        _switchOnLoad = -800;
        _switchOffLoad = 1000;

        _minimumCurrent = 6;
        _maximumCurrent = 16;
        _offCurrent = 5;

        _currentEntity = InputNumberEntity.Create(_testCtx.HaContext, "input_number.y");
        WithStateSensor(TextSensor.Create(_testCtx.HaContext, "sensor.z"));
        _cars = [];
        var _carCurrentEntity = InputNumberEntity.Create(_testCtx.HaContext, "input_number.my2024");
        var _carChargerSwitch = BinarySwitch.Create(_testCtx.HaContext, "switch.my2024_charge");

        AddCar("MY2024", CarChargingMode.Ac3Phase, _carChargerSwitch, _carCurrentEntity, 1, 16, 75, new NumericEntity(_testCtx.HaContext, "sensor.my2024_battery"), new NumericEntity(_testCtx.HaContext, "sensor.my2024_battery_max_charge"), remainOffAtFullBattery, new BinarySensor(_testCtx.HaContext, "binary_sensor.my2024_cable_connected"), new DeviceTracker(_testCtx.HaContext, "device_tracker.my2024_position"));
        return this;
    }

    public CarChargerEnergyConsumerBuilder WithName(String name)
    {
        _name = name;

        return this;
    }

    public CarChargerEnergyConsumerBuilder WithCriticalSensor(string sensorName)
    {
        _criticallyNeeded = BinarySensor.Create(_testCtx.HaContext, sensorName);

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

    public CarChargerEnergyConsumerBuilder AddTimeWindow(BinarySensor? isActive, TimeOnly start, TimeOnly end)
    {
        _timeWindows.Add(new TimeWindow(isActive, start, end));
        return this;
    }

    public CarChargerEnergyConsumerBuilder WithLoads(Double switchOn, Double switchOff)
    {
        _switchOnLoad = switchOn;
        _switchOffLoad = switchOff;
        return this;
    }

    public CarChargerEnergyConsumerBuilder WithStateSensor(TextSensor stateSensor)
    {
        _stateSensor = stateSensor;
        return this;
    }

    public CarChargerEnergyConsumerBuilder WithBalancingMethod(BalancingMethod balancingMethod)
    {
        _balancingMethod = balancingMethod;
        return this;
    }

    public CarChargerEnergyConsumerBuilder AddCar(String name, CarChargingMode mode, BinarySwitch? chargerSwitch, InputNumberEntity? currentEntity, Int32? minimumCurrent, Int32? maximumCurrent, Double batteryCapacity, NumericEntity batteryPercentageSensor, NumericEntity? maxBatteryPercentageSensor, bool remainOnAtFullBattery, BinarySensor cableConnectedSensor, DeviceTracker location)
    {
        _cars.Add(new Car(name, mode, chargerSwitch, currentEntity, minimumCurrent, maximumCurrent, batteryCapacity, batteryPercentageSensor, maxBatteryPercentageSensor, remainOnAtFullBattery, cableConnectedSensor, location, _testCtx.Scheduler));
        return this;
    }

    public CarChargerEnergyConsumer Build()
    {
        var x = new CarChargerEnergyConsumer(_logger, _name, _powerUsage, _criticallyNeeded, _switchOnLoad, _switchOffLoad, _minimumRuntime, _maximumRuntime, _minimumTimeout, _maximumTimeout, _timeWindows,
            _minimumCurrent, _maximumCurrent, _offCurrent, _currentEntity, _voltageEntity, _stateSensor, _cars, _testCtx.Scheduler);
        x.SetBalancingMethod(_testCtx.Scheduler.Now, _balancingMethod);
        return x;
    }
}