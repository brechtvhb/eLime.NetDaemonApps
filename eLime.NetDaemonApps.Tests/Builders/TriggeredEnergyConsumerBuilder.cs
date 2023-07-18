using eLime.NetDaemonApps.Domain.EnergyManager;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.TextSensors;
using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Tests.Helpers;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Tests.Builders;

public class TriggeredEnergyConsumerBuilder
{
    private readonly AppTestContext _testCtx;
    private String _name;

    private NumericEntity _powerUsage;
    private BinarySensor? _criticallyNeeded;
    public Boolean _preferSolar;
    private Double _switchOnLoad;

    private TimeSpan? _minimumRuntime;
    private TimeSpan? _maximumRuntime;
    private TimeSpan? _minimumTimeout;
    private TimeSpan? _maximumTimeout;
    private List<TimeWindow> _timeWindows = new();

    private BinarySwitch _socket;

    private TextSensor _stateSensor;
    private String _startState;
    private String _criticalState;
    private Boolean _canForceShutdown;
    private List<(String state, Double peakLoad)> _statePeakLoads = new();

    public TriggeredEnergyConsumerBuilder(AppTestContext testCtx, String baseType)
    {
        _testCtx = testCtx;

        if (baseType == "irrigation")
        {
            _name = "Irrigation";
            _powerUsage = new NumericEntity(_testCtx.HaContext, "sensor.socket_shed_pump_power");
            _preferSolar = true;
            _switchOnLoad = -700;

            _socket = BinarySwitch.Create(_testCtx.HaContext, "switch.irrigation_energy_available");

            WithStateSensor(TextSensor.Create(_testCtx.HaContext, "sensor.irrigation_state"), "Yes", "Critical");
            WithCanForceShutdown();
            AddStatePeakLoad("No", 1);
            AddStatePeakLoad("Yes", 700);
            AddStatePeakLoad("Critical", 700);
        }

    }

    public TriggeredEnergyConsumerBuilder WithName(String name)
    {
        _name = name;
        _socket = BinarySwitch.Create(_testCtx.HaContext, $"switch.socket_{name.MakeHaFriendly()}");
        _powerUsage = new NumericEntity(_testCtx.HaContext, $"sensor.socket_{name.MakeHaFriendly()}_power");

        return this;
    }

    public TriggeredEnergyConsumerBuilder WithCriticalSensor(string sensorName)
    {
        _criticallyNeeded = BinarySensor.Create(_testCtx.HaContext, sensorName);

        return this;
    }

    public TriggeredEnergyConsumerBuilder WithRuntime(TimeSpan? minimum, TimeSpan? maximum)
    {
        _minimumRuntime = minimum;
        _maximumRuntime = maximum;

        return this;
    }

    public TriggeredEnergyConsumerBuilder WithTimeout(TimeSpan? minimum, TimeSpan? maximum)
    {
        _minimumTimeout = minimum;
        _maximumTimeout = maximum;

        return this;
    }

    public TriggeredEnergyConsumerBuilder AddTimeWindow(BinarySensor? isActive, TimeOnly start, TimeOnly end)
    {
        _timeWindows.Add(new TimeWindow(isActive, start, end));
        return this;
    }

    public TriggeredEnergyConsumerBuilder WithPreferSolar()
    {
        _preferSolar = true;
        return this;
    }

    public TriggeredEnergyConsumerBuilder WithStateSensor(TextSensor stateSensor, String startState, String criticalState)
    {
        _stateSensor = stateSensor;
        _startState = startState;
        _criticalState = criticalState;
        return this;
    }

    public TriggeredEnergyConsumerBuilder AddStatePeakLoad(String state, Double peakLoad)
    {
        _statePeakLoads.Add((state, peakLoad));
        return this;
    }

    public TriggeredEnergyConsumerBuilder WithCanForceShutdown()
    {
        _canForceShutdown = true;
        return this;
    }

    public TriggeredEnergyConsumer Build()
    {
        var x = new TriggeredEnergyConsumer(_name, _powerUsage, _criticallyNeeded, _preferSolar, _switchOnLoad, _minimumRuntime, _maximumRuntime, _minimumTimeout, _maximumTimeout, _timeWindows, _socket, _statePeakLoads, _stateSensor, _startState, _criticalState, _canForceShutdown);
        return x;
    }
}