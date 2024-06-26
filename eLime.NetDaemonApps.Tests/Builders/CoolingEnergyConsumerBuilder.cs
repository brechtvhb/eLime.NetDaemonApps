using eLime.NetDaemonApps.Domain.EnergyManager;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Tests.Helpers;
using Microsoft.Extensions.Logging;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Tests.Builders;

public class CoolingEnergyConsumerBuilder
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
    private List<TimeWindow> _timeWindows = [];
    private String _timezone;

    private BinarySwitch _socket;
    private Double _peakLoad;

    private NumericEntity _temperatureSensor;
    private Double _targetTemperature;
    private Double _maxTemperature;

    public CoolingEnergyConsumerBuilder(ILogger logger, AppTestContext testCtx)
    {
        _logger = logger;
        _testCtx = testCtx;
        _timezone = "Utc";

        _name = "Fridge";
        _powerUsage = new NumericEntity(_testCtx.HaContext, "sensor.socket_fridge_power");
        _switchOnLoad = -50;
        _switchOffLoad = 200;

        _socket = BinarySwitch.Create(_testCtx.HaContext, "switch.socket_fridge");
        _peakLoad = 75;

        _temperatureSensor = new NumericEntity(_testCtx.HaContext, "sensor.fridge_temperature");
        _targetTemperature = 0.5;
        _maxTemperature = 8.5;
    }

    public CoolingEnergyConsumerBuilder WithName(String name)
    {
        _name = name;
        _socket = BinarySwitch.Create(_testCtx.HaContext, $"switch.socket_{name.MakeHaFriendly()}");
        _powerUsage = new NumericEntity(_testCtx.HaContext, $"sensor.socket_{name.MakeHaFriendly()}_power");

        return this;
    }

    public CoolingEnergyConsumerBuilder WithCriticalSensor(string sensorName)
    {
        _criticallyNeeded = BinarySensor.Create(_testCtx.HaContext, sensorName);

        return this;
    }

    public CoolingEnergyConsumerBuilder WithRuntime(TimeSpan? minimum, TimeSpan? maximum)
    {
        _minimumRuntime = minimum;
        _maximumRuntime = maximum;

        return this;
    }

    public CoolingEnergyConsumerBuilder WithTimeout(TimeSpan? minimum, TimeSpan? maximum)
    {
        _minimumTimeout = minimum;
        _maximumTimeout = maximum;

        return this;
    }

    public CoolingEnergyConsumerBuilder WithLoad(Double switchOnLoad, Double switchOffLoad, Double peakLoad)
    {
        _switchOnLoad = switchOnLoad;
        _peakLoad = peakLoad;

        return this;
    }


    public CoolingEnergyConsumerBuilder AddTimeWindow(BinarySensor? isActive, TimeOnly start, TimeOnly end)
    {
        _timeWindows.Add(new TimeWindow(isActive, start, end));
        return this;
    }

    public CoolingEnergyConsumerBuilder WithTimezone(String timezone)
    {
        _timezone = timezone;
        return this;
    }

    public CoolingEnergyConsumerBuilder WithTemperatureSensor(string sensorName, double targetTemperature, double maxTemperature)
    {
        _temperatureSensor = new NumericEntity(_testCtx.HaContext, sensorName);
        _targetTemperature = targetTemperature;
        _maxTemperature = maxTemperature;

        return this;
    }

    public CoolingEnergyConsumer Build()
    {
        var x = new CoolingEnergyConsumer(_logger, _name, _powerUsage, _criticallyNeeded, _switchOnLoad, _switchOffLoad, _minimumRuntime, _maximumRuntime, _minimumTimeout, _maximumTimeout, _timeWindows, _timezone, _socket, _peakLoad, _temperatureSensor, _targetTemperature, _maxTemperature);
        return x;
    }
}