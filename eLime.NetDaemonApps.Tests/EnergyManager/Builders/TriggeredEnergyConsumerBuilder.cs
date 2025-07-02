using eLime.NetDaemonApps.Config.EnergyManager;
using eLime.NetDaemonApps.Domain.Helper;
#pragma warning disable CS8618, CS9264

namespace eLime.NetDaemonApps.Tests.EnergyManager.Builders;

public class TriggeredEnergyConsumerBuilder
{
    private string _name;
    private readonly List<string> _consumerGroups = [];

    private string _powerUsage;
    private string? _criticallyNeeded;
    private double _switchOnLoad;
    private double _switchOffLoad;
    private List<LoadTimeFrames> _loadTimeFramesToCheckOnStart = [];
    private List<LoadTimeFrames> _loadTimeFramesToCheckOnStop = [];

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
    private readonly List<TriggeredEnergyConsumerStateConfig> _states = [];

    public TriggeredEnergyConsumerBuilder()
    {
    }

    public static TriggeredEnergyConsumerBuilder Irrigation =>
        new TriggeredEnergyConsumerBuilder()
            .WithName("Irrigation")
            .WithSocket("switch.irrigation_energy_available")
            .WithLoad(-700, [LoadTimeFrames.Now, LoadTimeFrames.Last30Seconds], 200, [LoadTimeFrames.Now, LoadTimeFrames.Last30Seconds])
            .AddConsumerGroup("Deferrable")
            .WithStateSensor("sensor.irrigation_state", "Yes", null, "No", "Critical")
            .AddStatePeakLoad("No", 1, false)
            .AddStatePeakLoad("Yes", 1, false)
            .AddStatePeakLoad("Critical", 1, false)
            .AddStatePeakLoad("Ongoing", 700, true);

    public static TriggeredEnergyConsumerBuilder Washer =>
        new TriggeredEnergyConsumerBuilder()
            .WithName("Washer")
            .WithSocket("switch.washer_socket")
            .WithShutdownOnComplete()
            .WithLoad(-700, [LoadTimeFrames.Now, LoadTimeFrames.Last30Seconds], 5000, [LoadTimeFrames.Now, LoadTimeFrames.Last30Seconds])
            .AddConsumerGroup("Deferrable")
            .WithStateSensor("sensor.smartwasher_smartwasher_state", "DelayedStart", null, "Ready", "Critical")
            .AddStatePeakLoad("Idle", 0, false)
            .AddStatePeakLoad("DelayedStart", 0, false)
            .AddStatePeakLoad("Prewashing", 120, true)
            .AddStatePeakLoad("Heating", 2200, true)
            .AddStatePeakLoad("Washing", 170, true)
            .AddStatePeakLoad("Rinsing", 330, true)
            .AddStatePeakLoad("Spinning", 420, true);

    public static TriggeredEnergyConsumerBuilder Dryer =>
        new TriggeredEnergyConsumerBuilder()
            .WithName("Dryer")
            .WithLoad(-500, [LoadTimeFrames.Now, LoadTimeFrames.Last30Seconds], 3500, [LoadTimeFrames.Now, LoadTimeFrames.Last30Seconds])
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

    public static TriggeredEnergyConsumerBuilder Dishwasher =>
        new TriggeredEnergyConsumerBuilder()
            .WithName("Dishwasher")
            .WithSocket("switch.dishwasher_socket")
            .WithLoad(-700, [LoadTimeFrames.Now, LoadTimeFrames.Last30Seconds], 3000, [LoadTimeFrames.Now, LoadTimeFrames.Last30Seconds])
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

    public TriggeredEnergyConsumerBuilder WithName(string name)
    {
        _name = name;
        _powerUsage = $"sensor.socket_{name.MakeHaFriendly()}_power";

        return this;
    }
    public TriggeredEnergyConsumerBuilder WithSocket(string socket)
    {
        _socket = socket;
        return this;
    }

    public TriggeredEnergyConsumerBuilder AddConsumerGroup(string consumerGroup)
    {
        _consumerGroups.Add(consumerGroup);
        return this;
    }

    public TriggeredEnergyConsumerBuilder WithCriticalSensor(string sensorName)
    {
        _criticallyNeeded = sensorName;

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

    public TriggeredEnergyConsumerBuilder AddTimeWindow(string? isActive, TimeSpan start, TimeSpan end)
    {
        _timeWindows.Add(new TimeWindowConfig
        {
            ActiveSensor = isActive,
            Start = start,
            End = end
        });
        return this;
    }

    public TriggeredEnergyConsumerBuilder WithLoad(double switchOn, List<LoadTimeFrames> loadTimeFramesToCheckOnStart, double switchOff, List<LoadTimeFrames> loadTimeFramesToCheckOnStop)
    {
        _switchOnLoad = switchOn;
        _loadTimeFramesToCheckOnStart = loadTimeFramesToCheckOnStart;
        _switchOffLoad = switchOff;
        _loadTimeFramesToCheckOnStop = loadTimeFramesToCheckOnStop;

        return this;
    }

    public TriggeredEnergyConsumerBuilder WithStateSensor(string stateSensor, string startState, string? pausedState, string completedState, string? criticalState)
    {
        _stateSensor = stateSensor;
        _startState = startState;
        _pausedState = pausedState;
        _completedState = completedState;
        _criticalState = criticalState;
        return this;
    }

    public TriggeredEnergyConsumerBuilder WithPauseSwitch(string pauseSwitch)
    {
        _pauseSwitch = pauseSwitch;
        return this;
    }

    public TriggeredEnergyConsumerBuilder WithStartButton(string startButton)
    {
        _startButton = startButton;
        return this;
    }

    public TriggeredEnergyConsumerBuilder AddStatePeakLoad(string name, double peakLoad, bool isRunning)
    {
        _states.Add(new TriggeredEnergyConsumerStateConfig { IsRunning = isRunning, Name = name, PeakLoad = peakLoad });
        return this;
    }


    public TriggeredEnergyConsumerBuilder WithShutdownOnComplete()
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
            LoadTimeFramesToCheckOnStart = _loadTimeFramesToCheckOnStart,
            LoadTimeFramesToCheckOnStop = _loadTimeFramesToCheckOnStop,
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