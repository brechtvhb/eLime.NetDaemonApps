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
    private Double _switchOnLoad;
    private Double _switchOffLoad;

    private TimeSpan? _minimumRuntime;
    private TimeSpan? _maximumRuntime;
    private TimeSpan? _minimumTimeout;
    private TimeSpan? _maximumTimeout;
    private List<TimeWindow> _timeWindows = new();

    private BinarySwitch _socket;

    private TextSensor _stateSensor;
    private String _startState;
    private String _completedState;
    private String _criticalState;
    private Boolean _canForceShutdown;
    private Boolean _shutDownOnComplete;
    private List<(String state, Double peakLoad)> _statePeakLoads = new();

    public TriggeredEnergyConsumerBuilder(AppTestContext testCtx, String baseType)
    {
        _testCtx = testCtx;

        if (baseType == "irrigation")
        {
            _name = "Irrigation";
            _powerUsage = new NumericEntity(_testCtx.HaContext, "sensor.socket_shed_pump_power");
            _switchOnLoad = -700;
            _switchOffLoad = 200;

            _socket = BinarySwitch.Create(_testCtx.HaContext, "switch.irrigation_energy_available");

            WithStateSensor(TextSensor.Create(_testCtx.HaContext, "sensor.irrigation_state"), "Yes", "No", "Critical");
            WithCanForceShutdown();
            AddStatePeakLoad("No", 1);
            AddStatePeakLoad("Yes", 1);
            AddStatePeakLoad("Critical", 1);
            AddStatePeakLoad("Ongoing", 700);
        }

        if (baseType == "washer")
        {
            _name = "Washer";
            _powerUsage = new NumericEntity(_testCtx.HaContext, "sensor.socket_washer");
            _switchOnLoad = -700;
            _switchOffLoad = 5000;

            _socket = BinarySwitch.Create(_testCtx.HaContext, "switch.smartwasher_smartwasher_delayed_start_activate");

            WithStateSensor(TextSensor.Create(_testCtx.HaContext, "sensor.smartwasher_smartwasher_state"), "DelayedStart", "Ready", "Critical");
            WithCanForceShutdown();
            AddStatePeakLoad("Idle", 0);
            AddStatePeakLoad("DelayedStart", 0);
            AddStatePeakLoad("Prewashing", 120);
            AddStatePeakLoad("Heating", 2200);
            AddStatePeakLoad("Washing", 170);
            AddStatePeakLoad("Rinsing", 330);
            AddStatePeakLoad("Spinning", 420);
        }

        WithShutdownOnComplete();
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

    public TriggeredEnergyConsumerBuilder WithLoads(Double switchOn, Double switchOff)
    {
        _switchOnLoad = switchOn;
        _switchOffLoad = switchOff;
        return this;
    }

    public TriggeredEnergyConsumerBuilder WithStateSensor(TextSensor stateSensor, String startState, String completedState, String criticalState)
    {
        _stateSensor = stateSensor;
        _startState = startState;
        _completedState = completedState;
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
    public TriggeredEnergyConsumerBuilder WithShutdownOnComplete()
    {
        _shutDownOnComplete = true;
        return this;
    }

    public TriggeredEnergyConsumer Build()
    {
        var x = new TriggeredEnergyConsumer(_name, _powerUsage, _criticallyNeeded, _switchOnLoad, _switchOffLoad, _minimumRuntime, _maximumRuntime, _minimumTimeout, _maximumTimeout, _timeWindows, _socket, _statePeakLoads, _stateSensor, _startState, _completedState, _criticalState, _canForceShutdown, _shutDownOnComplete);
        return x;
    }
}