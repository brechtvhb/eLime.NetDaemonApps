using eLime.NetDaemonApps.Domain.EnergyManager;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.TextSensors;
using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Tests.Helpers;
using Microsoft.Extensions.Logging;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Tests.Builders;

public class TriggeredEnergyConsumerBuilder
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
    private string _timezone;

    private BinarySwitch _socket;
    private BinarySwitch? _pauseSwitch;

    private TextSensor _stateSensor;
    private String _startState;
    private String? _pausedState;
    private String _completedState;
    private String _criticalState;
    private Boolean _canForceShutdown;
    private Boolean _shutDownOnComplete;
    private List<(String state, Double peakLoad)> _statePeakLoads = new();

    public TriggeredEnergyConsumerBuilder(ILogger logger, AppTestContext testCtx, String baseType)
    {
        _logger = logger;
        _testCtx = testCtx;
        _timezone = "Utc";

        if (baseType == "irrigation")
        {
            _name = "Irrigation";
            _powerUsage = new NumericEntity(_testCtx.HaContext, "sensor.socket_shed_pump_power");
            _switchOnLoad = -700;
            _switchOffLoad = 200;

            _socket = BinarySwitch.Create(_testCtx.HaContext, "switch.irrigation_energy_available");

            WithStateSensor(TextSensor.Create(_testCtx.HaContext, "sensor.irrigation_state"), "Yes", null, "No", "Critical");
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

            WithStateSensor(TextSensor.Create(_testCtx.HaContext, "sensor.smartwasher_smartwasher_state"), "DelayedStart", null, "Ready", "Critical");
            WithCanForceShutdown();
            AddStatePeakLoad("Idle", 0);
            AddStatePeakLoad("DelayedStart", 0);
            AddStatePeakLoad("Prewashing", 120);
            AddStatePeakLoad("Heating", 2200);
            AddStatePeakLoad("Washing", 170);
            AddStatePeakLoad("Rinsing", 330);
            AddStatePeakLoad("Spinning", 420);
        }
        if (baseType == "dishwasher")
        {
            _name = "Dishwasher";
            _powerUsage = new NumericEntity(_testCtx.HaContext, "sensor.socket_dishwasher");
            _switchOnLoad = -700;
            _switchOffLoad = 3000;

            _socket = BinarySwitch.Create(_testCtx.HaContext, "switch.dishwasher_program_auto2");
            _pauseSwitch = BinarySwitch.Create(_testCtx.HaContext, "switch.dishwasher_power");

            WithStateSensor(TextSensor.Create(_testCtx.HaContext, "sensor.dishwasher_operation_state_enhanced"), "DelayedStart", "Paused", "Ready", "Critical");
            WithCanForceShutdown();
            AddStatePeakLoad("Inactive", 1);
            AddStatePeakLoad("Aborting", 1);
            AddStatePeakLoad("DelayedStart", 1);
            AddStatePeakLoad("RemoteStart", 1);
            AddStatePeakLoad("Ready", 1);
            AddStatePeakLoad("Running", 1500);
            AddStatePeakLoad("Drying", 15);
            AddStatePeakLoad("Finished", 1);
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
    public TriggeredEnergyConsumerBuilder WithTimezone(String timezone)
    {
        _timezone = timezone;
        return this;
    }


    public TriggeredEnergyConsumerBuilder WithLoads(Double switchOn, Double switchOff)
    {
        _switchOnLoad = switchOn;
        _switchOffLoad = switchOff;
        return this;
    }

    public TriggeredEnergyConsumerBuilder WithStateSensor(TextSensor stateSensor, String startState, String? pausedState, String completedState, String criticalState)
    {
        _stateSensor = stateSensor;
        _startState = startState;
        _pausedState = pausedState;
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
        var x = new TriggeredEnergyConsumer(_logger, _name, _powerUsage, _criticallyNeeded, _switchOnLoad, _switchOffLoad, _minimumRuntime, _maximumRuntime, _minimumTimeout, _maximumTimeout, _timeWindows, _timezone, _socket, _pauseSwitch, _statePeakLoads, 0, _stateSensor, _startState, _pausedState, _completedState, _criticalState, _canForceShutdown, _shutDownOnComplete);
        return x;
    }
}