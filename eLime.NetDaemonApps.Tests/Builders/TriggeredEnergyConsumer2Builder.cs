using eLime.NetDaemonApps.Config.EnergyManager;
using eLime.NetDaemonApps.Domain.Helper;
#pragma warning disable CS8618, CS9264

namespace eLime.NetDaemonApps.Tests.Builders;

public class TriggeredEnergyConsumer2Builder
{
    private string _name;
    private readonly List<string> _consumerGroups = [];

    private string _powerUsage;
    private string? _criticallyNeeded;
    private double _switchOnLoad;
    private double _switchOffLoad;

    private TimeSpan? _minimumRuntime;
    private TimeSpan? _maximumRuntime;
    private TimeSpan? _minimumTimeout;
    private TimeSpan? _maximumTimeout;
    private readonly List<TimeWindowConfig> _timeWindows = [];

    private string _socket;
    private string? _startButton;
    private string? _pauseSwitch;

    private string _stateSensor;
    private string _startState;
    private string? _pausedState;
    private string _completedState;
    private string? _criticalState;
    private bool _shutDownOnComplete;
    private readonly List<State> _states = [];

    public TriggeredEnergyConsumer2Builder()
    {
    }

    public static TriggeredEnergyConsumer2Builder Irrigation =>
        new TriggeredEnergyConsumer2Builder()
            .WithName("Irrigation")
            .WithSocket("switch.irrigation_energy_available")
            .WithLoads(-700, 200)
            .AddConsumerGroup("Deferrable")
            .WithStateSensor("sensor.irrigation_state", "Yes", null, "No", "Critical")
            .AddStatePeakLoad("No", 1, false)
            .AddStatePeakLoad("Yes", 1, false)
            .AddStatePeakLoad("Critical", 1, false)
            .AddStatePeakLoad("Ongoing", 700, true);

    public static TriggeredEnergyConsumer2Builder Washer =>
        new TriggeredEnergyConsumer2Builder()
            .WithName("Washer")
            .WithSocket("switch.washer_socket")
            .WithShutdownOnComplete()
            .WithLoads(-700, 5000)
            .AddConsumerGroup("Deferrable")
            .WithStateSensor("sensor.smartwasher_smartwasher_state", "DelayedStart", null, "Ready", "Critical")
            .AddStatePeakLoad("Idle", 0, false)
            .AddStatePeakLoad("DelayedStart", 0, false)
            .AddStatePeakLoad("Prewashing", 120, true)
            .AddStatePeakLoad("Heating", 2200, true)
            .AddStatePeakLoad("Washing", 170, true)
            .AddStatePeakLoad("Rinsing", 330, true)
            .AddStatePeakLoad("Spinning", 420, true);

    public static TriggeredEnergyConsumer2Builder Dryer =>
        new TriggeredEnergyConsumer2Builder()
            .WithName("Dryer")
            .WithLoads(-500, 3500)
            .AddConsumerGroup("Deferrable")
            .WithStateSensor("sensor.dryer", "waiting_to_start", null, "program_ended", null)
            .WithStartButton("button.dryer_start")
            .AddStatePeakLoad("off", 1, false)
            .AddStatePeakLoad("on", 4, false)
            .AddStatePeakLoad("waiting_to_start", 4, false)
            .AddStatePeakLoad("drying", 450, true)
            .AddStatePeakLoad("machine_iron", 470, true)
            .AddStatePeakLoad("hand_iron_2", 480, true)
            .AddStatePeakLoad("hand_iron_1", 500, true)
            .AddStatePeakLoad("program_ended", 100, true);

    public static TriggeredEnergyConsumer2Builder Dishwasher =>
        new TriggeredEnergyConsumer2Builder()
            .WithName("Dishwasher")
            .WithSocket("switch.dishwasher_socket")
            .WithLoads(-700, 3000)
            .AddConsumerGroup("Deferrable")
            .WithStateSensor("sensor.dishwasher_operation_state_enhanced", "DelayedStart", "Paused", "Ready", "Critical")
            .WithPauseSwitch("switch.dishwasher_pause")
            .AddStatePeakLoad("Inactive", 1, false)
            .AddStatePeakLoad("Aborting", 1, false)
            .AddStatePeakLoad("Paused", 1, false)
            .AddStatePeakLoad("DelayedStart", 1, false)
            .AddStatePeakLoad("RemoteStart", 1, false)
            .AddStatePeakLoad("Ready", 1, false)
            .AddStatePeakLoad("Running", 1500, true)
            .AddStatePeakLoad("Drying", 15, true)
            .AddStatePeakLoad("Finished", 1, false)
            .WithShutdownOnComplete();

    public TriggeredEnergyConsumer2Builder WithName(string name)
    {
        _name = name;
        _powerUsage = $"sensor.socket_{name.MakeHaFriendly()}_power";

        return this;
    }
    public TriggeredEnergyConsumer2Builder WithSocket(string socket)
    {
        _socket = socket;
        return this;
    }

    public TriggeredEnergyConsumer2Builder AddConsumerGroup(string consumerGroup)
    {
        _consumerGroups.Add(consumerGroup);
        return this;
    }

    public TriggeredEnergyConsumer2Builder WithCriticalSensor(string sensorName)
    {
        _criticallyNeeded = sensorName;

        return this;
    }

    public TriggeredEnergyConsumer2Builder WithRuntime(TimeSpan? minimum, TimeSpan? maximum)
    {
        _minimumRuntime = minimum;
        _maximumRuntime = maximum;

        return this;
    }

    public TriggeredEnergyConsumer2Builder WithTimeout(TimeSpan? minimum, TimeSpan? maximum)
    {
        _minimumTimeout = minimum;
        _maximumTimeout = maximum;

        return this;
    }

    public TriggeredEnergyConsumer2Builder AddTimeWindow(string? isActive, TimeSpan start, TimeSpan end)
    {
        _timeWindows.Add(new TimeWindowConfig
        {
            ActiveSensor = isActive,
            Start = start,
            End = end
        });
        return this;
    }

    public TriggeredEnergyConsumer2Builder WithLoads(double switchOn, double switchOff)
    {
        _switchOnLoad = switchOn;
        _switchOffLoad = switchOff;
        return this;
    }

    public TriggeredEnergyConsumer2Builder WithStateSensor(string stateSensor, string startState, string? pausedState, string completedState, string? criticalState)
    {
        _stateSensor = stateSensor;
        _startState = startState;
        _pausedState = pausedState;
        _completedState = completedState;
        _criticalState = criticalState;
        return this;
    }

    public TriggeredEnergyConsumer2Builder WithPauseSwitch(string pauseSwitch)
    {
        _pauseSwitch = pauseSwitch;
        return this;
    }

    public TriggeredEnergyConsumer2Builder WithStartButton(string startButton)
    {
        _startButton = startButton;
        return this;
    }

    public TriggeredEnergyConsumer2Builder AddStatePeakLoad(string name, double peakLoad, bool isRunning)
    {
        _states.Add(new State { IsRunning = isRunning, Name = name, PeakLoad = peakLoad });
        return this;
    }


    public TriggeredEnergyConsumer2Builder WithShutdownOnComplete()
    {
        _shutDownOnComplete = true;
        return this;
    }

    public EnergyConsumerConfig Build()
    {
        return new EnergyConsumerConfig
        {
            Name = _name,
            PowerUsageEntity = _powerUsage,
            ConsumerGroups = _consumerGroups,
            SwitchOnLoad = _switchOnLoad,
            SwitchOffLoad = _switchOffLoad,
            MinimumRuntime = _minimumRuntime,
            MaximumRuntime = _maximumRuntime,
            MinimumTimeout = _minimumTimeout,
            MaximumTimeout = _maximumTimeout,
            TimeWindows = _timeWindows,
            CriticallyNeededEntity = _criticallyNeeded,

            Triggered = new TriggeredEnergyConsumerConfig
            {
                SocketEntity = _socket,
                StartButton = _startButton,
                PauseSwitch = _pauseSwitch,
                StateSensor = _stateSensor,
                StartState = _startState,
                PausedState = _pausedState,
                CompletedState = _completedState,
                CriticalState = _criticalState,
                CanPause = _pauseSwitch != null,
                ShutDownOnComplete = _shutDownOnComplete,
                States = _states
            }
        };
    }
}