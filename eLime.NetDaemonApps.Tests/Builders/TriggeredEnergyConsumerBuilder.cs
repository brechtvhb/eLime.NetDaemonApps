using eLime.NetDaemonApps.Domain.EnergyManager;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.Buttons;
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
    private string _name;
    private List<string> _consumerGroups = [];

    private NumericEntity _powerUsage;
    private BinarySensor? _criticallyNeeded;
    private double _switchOnLoad;
    private double _switchOffLoad;

    private TimeSpan? _minimumRuntime;
    private TimeSpan? _maximumRuntime;
    private TimeSpan? _minimumTimeout;
    private TimeSpan? _maximumTimeout;
    private List<TimeWindow> _timeWindows = [];
    private string _timezone;

    private BinarySwitch _socket;
    private Button? _startButton;
    private BinarySwitch? _pauseSwitch;

    private TextSensor _stateSensor;
    private string _startState;
    private string? _pausedState;
    private string _completedState;
    private string? _criticalState;
    private bool _canForceShutdown;
    private bool _shutDownOnComplete;
    private List<State> _states = [];

    public TriggeredEnergyConsumerBuilder(ILogger logger, AppTestContext testCtx, string baseType)
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
            AddConsumerGroup("Deferrable");

            WithStateSensor(TextSensor.Create(_testCtx.HaContext, "sensor.irrigation_state"), "Yes", null, "No", "Critical");
            WithCanForceShutdown();
            AddStatePeakLoad("No", 1, false);
            AddStatePeakLoad("Yes", 1, false);
            AddStatePeakLoad("Critical", 1, false);
            AddStatePeakLoad("Ongoing", 700, true);
        }

        if (baseType == "washer")
        {
            _name = "Washer";
            _powerUsage = new NumericEntity(_testCtx.HaContext, "sensor.socket_washer");
            _switchOnLoad = -700;
            _switchOffLoad = 5000;

            _socket = BinarySwitch.Create(_testCtx.HaContext, "switch.smartwasher_smartwasher_delayed_start_activate");
            AddConsumerGroup("Deferrable");

            WithStateSensor(TextSensor.Create(_testCtx.HaContext, "sensor.smartwasher_smartwasher_state"), "DelayedStart", null, "Ready", "Critical");
            WithCanForceShutdown();
            AddStatePeakLoad("Idle", 0, false);
            AddStatePeakLoad("DelayedStart", 0, false);
            AddStatePeakLoad("Prewashing", 120, true);
            AddStatePeakLoad("Heating", 2200, true);
            AddStatePeakLoad("Washing", 170, true);
            AddStatePeakLoad("Rinsing", 330, true);
            AddStatePeakLoad("Spinning", 420, true);
        }

        if (baseType == "tumble_dryer")
        {
            _name = "Washer";
            _powerUsage = new NumericEntity(_testCtx.HaContext, "sensor.socket_dryer_power");
            _switchOnLoad = -500;
            _switchOffLoad = 3500;
            _startButton = new Button(_testCtx.HaContext, "button.tumble_dryer_start");
            _socket = BinarySwitch.Create(_testCtx.HaContext, "switch.tumble_dryer_power_on");
            AddConsumerGroup("Deferrable");

            WithStateSensor(TextSensor.Create(_testCtx.HaContext, "sensor.tumble_dryer_state"), "waiting_to_start", null, "program_ended", null);
            WithCanForceShutdown();
            AddStatePeakLoad("off", 1, false);
            AddStatePeakLoad("on", 4, false);
            AddStatePeakLoad("waiting_to_start", 4, false);
            AddStatePeakLoad("drying", 450, true);
            AddStatePeakLoad("machine_iron", 470, true);
            AddStatePeakLoad("hand_iron_2", 480, true);
            AddStatePeakLoad("hand_iron_1", 500, true);
            AddStatePeakLoad("program_ended", 100, true);
        }

        if (baseType == "dishwasher")
        {
            _name = "Dishwasher";
            _powerUsage = new NumericEntity(_testCtx.HaContext, "sensor.socket_dishwasher");
            _switchOnLoad = -700;
            _switchOffLoad = 3000;

            _socket = BinarySwitch.Create(_testCtx.HaContext, "switch.dishwasher_program_auto2");
            _pauseSwitch = BinarySwitch.Create(_testCtx.HaContext, "switch.dishwasher_power");
            AddConsumerGroup("Deferrable");

            WithStateSensor(TextSensor.Create(_testCtx.HaContext, "sensor.dishwasher_operation_state_enhanced"), "DelayedStart", "Paused", "Ready", "Critical");
            WithCanForceShutdown();
            AddStatePeakLoad("Inactive", 1, false);
            AddStatePeakLoad("Aborting", 1, false);
            AddStatePeakLoad("DelayedStart", 1, false);
            AddStatePeakLoad("RemoteStart", 1, false);
            AddStatePeakLoad("Ready", 1, false);
            AddStatePeakLoad("Running", 1500, true);
            AddStatePeakLoad("Drying", 15, true);
            AddStatePeakLoad("Finished", 1, false);
        }

        WithShutdownOnComplete();
    }

    public TriggeredEnergyConsumerBuilder WithName(string name)
    {
        _name = name;
        _socket = BinarySwitch.Create(_testCtx.HaContext, $"switch.socket_{name.MakeHaFriendly()}");
        _powerUsage = new NumericEntity(_testCtx.HaContext, $"sensor.socket_{name.MakeHaFriendly()}_power");

        return this;
    }
    public TriggeredEnergyConsumerBuilder AddConsumerGroup(string consumerGroup)
    {
        _consumerGroups.Add(consumerGroup);
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
    public TriggeredEnergyConsumerBuilder WithTimezone(string timezone)
    {
        _timezone = timezone;
        return this;
    }


    public TriggeredEnergyConsumerBuilder WithLoads(double switchOn, double switchOff)
    {
        _switchOnLoad = switchOn;
        _switchOffLoad = switchOff;
        return this;
    }

    public TriggeredEnergyConsumerBuilder WithStateSensor(TextSensor stateSensor, string startState, string? pausedState, string completedState, string? criticalState)
    {
        _stateSensor = stateSensor;
        _startState = startState;
        _pausedState = pausedState;
        _completedState = completedState;
        _criticalState = criticalState;
        return this;
    }

    public TriggeredEnergyConsumerBuilder AddStatePeakLoad(string name, double peakLoad, bool isRunning)
    {
        _states.Add(State.Create(name, peakLoad, isRunning));
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
        var x = new TriggeredEnergyConsumer(_logger, _name, _consumerGroups, _powerUsage, _criticallyNeeded, _switchOnLoad, _switchOffLoad, _minimumRuntime, _maximumRuntime, _minimumTimeout, _maximumTimeout, _timeWindows, _timezone, _socket, _startButton, _pauseSwitch, _states, _stateSensor, _startState, _pausedState, _completedState, _criticalState, _canForceShutdown, _shutDownOnComplete);
        return x;
    }
}