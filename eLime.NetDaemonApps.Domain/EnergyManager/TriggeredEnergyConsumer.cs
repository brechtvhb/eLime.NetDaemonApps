using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.Buttons;
using eLime.NetDaemonApps.Domain.Entities.TextSensors;
using Microsoft.Extensions.Logging;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.EnergyManager;

public class TriggeredEnergyConsumer : EnergyConsumer
{
    public BinarySwitch Socket { get; }
    public Button? StartButton { get; }

    public BinarySwitch? PauseSwitch { get; }
    public override bool Running => Socket.IsOn() && StateSensor.State != StartState && StateSensor.State != CompletedState && StatePeakLoads.Select(x => x.State).Contains(StateSensor.State);


    public TextSensor StateSensor { get; }
    public String StartState { get; }
    public String? PausedState { get; }
    public String CompletedState { get; }
    public String? CriticalState { get; }
    public Boolean CanPause { get; }
    public Boolean ShutDownOnComplete { get; }

    public List<(String State, Double PeakLoad)> StatePeakLoads { get; }
    public double defaultLoad;

    public override double PeakLoad
    {
        get
        {
            var currentState = StateSensor.State;

            if (StatePeakLoads.All(x => x.State != currentState))
                return defaultLoad;

            var currentStatePassed = false;
            var maxPeakLoad = 0d;

            foreach (var (state, peakLoad) in StatePeakLoads)
            {
                if (state == currentState)
                    currentStatePassed = true;

                if (currentStatePassed && peakLoad > maxPeakLoad)
                    maxPeakLoad = peakLoad;
            }
            return maxPeakLoad;
        }
    }

    public TriggeredEnergyConsumer(ILogger logger, String name, List<string> consumerGroups, NumericEntity powerUsage, BinarySensor? criticallyNeeded, Double switchOnLoad, Double switchOffLoad, TimeSpan? minimumRuntime, TimeSpan? maximumRuntime, TimeSpan? minimumTimeout,
        TimeSpan? maximumTimeout, List<TimeWindow> timeWindows, String timezone, BinarySwitch socket, Button? startButton, BinarySwitch? pauseSwitch, List<(String State, Double PeakLoad)> peakLoads, double defaultLoad, TextSensor stateSensor, String startState, String? pausedState, String completedState, String? criticalState, bool canPause, bool shutDownOnComplete)
    {
        SetCommonFields(logger, name, consumerGroups, powerUsage, criticallyNeeded, switchOnLoad, switchOffLoad, minimumRuntime, maximumRuntime, minimumTimeout, maximumTimeout, timeWindows, timezone);
        Socket = socket;
        Socket.TurnedOn += Socket_TurnedOn;
        Socket.TurnedOff += Socket_TurnedOff;

        StartButton = startButton;

        PauseSwitch = pauseSwitch;

        StateSensor = stateSensor;
        StateSensor.StateChanged += StateSensor_StateChanged;

        StartState = startState;
        PausedState = pausedState;
        CompletedState = completedState;
        CriticalState = criticalState;

        StatePeakLoads = peakLoads;
        CanPause = canPause;
        ShutDownOnComplete = shutDownOnComplete;
    }

    protected override EnergyConsumerState GetDesiredState(DateTimeOffset? now)
    {
        return Running switch
        {
            true when StateSensor.State == CompletedState => EnergyConsumerState.Off,
            true when StateSensor.State == PausedState => EnergyConsumerState.NeedsEnergy,
            true => EnergyConsumerState.Running,
            false when (StateSensor.State == StartState || StateSensor.State == PausedState) && CriticallyNeeded != null && CriticallyNeeded.IsOn() => EnergyConsumerState.CriticallyNeedsEnergy,
            false when StateSensor.State == CriticalState && !String.IsNullOrWhiteSpace(CriticalState) => EnergyConsumerState.CriticallyNeedsEnergy,
            false when (StateSensor.State == StartState || StateSensor.State == PausedState) => EnergyConsumerState.NeedsEnergy,
            false => EnergyConsumerState.Off,
        };
    }


    public override bool CanStart(DateTimeOffset now)
    {
        if (StateSensor.State == PausedState)
            return true;

        if (State is EnergyConsumerState.Running or EnergyConsumerState.Off)
            return false;

        if (!IsWithinTimeWindow(now) && HasTimeWindow())
            return false;

        if (MinimumTimeout == null)
            return true;

        return !(LastRun?.Add(MinimumTimeout.Value) > now);
    }

    public override bool CanForceStop(DateTimeOffset now)
    {
        if (MinimumRuntime != null && StartedAt?.Add(MinimumRuntime.Value) > now)
            return false;

        if (CriticallyNeeded != null && CriticallyNeeded.IsOn())
            return false;

        if (!CanPause)
            return false;

        return true;
    }

    public override bool CanForceStopOnPeakLoad(DateTimeOffset now)
    {
        if (MinimumRuntime != null && StartedAt?.Add(MinimumRuntime.Value) > now)
            return false;

        if (!CanPause)
            return false;

        return true;
    }

    public override void TurnOn()
    {
        if (StateSensor.State == PausedState && CanPause && PauseSwitch != null)
        {
            PauseSwitch.TurnOn();
            return;
        }

        if (StateSensor.State == StartState && StartButton != null)
        {
            StartButton.Press();
            return;
        }

        Socket.TurnOn();
    }

    public override void TurnOff()
    {
        if (StateSensor.State != CompletedState && CanPause && PauseSwitch != null)
        {
            PauseSwitch.TurnOff();
            return;
        }

        if (ShutDownOnComplete)
            Socket.TurnOff();
    }

    public override void DisposeInternal()
    {
        Socket.TurnedOn -= Socket_TurnedOn;
        Socket.TurnedOn -= Socket_TurnedOff;
        Socket.Dispose();

        PauseSwitch?.Dispose();

        StateSensor.StateChanged -= StateSensor_StateChanged;
        StateSensor.Dispose();
    }

    private void StateSensor_StateChanged(object? sender, TextSensorEventArgs e)
    {
        if (e.Sensor.State == StartState)
        {
            CheckDesiredState(new EnergyConsumerStartCommand(this, EnergyConsumerState.NeedsEnergy));
        }
        else if (e.Sensor.State == CriticalState)
        {
            CheckDesiredState(new EnergyConsumerStartCommand(this, EnergyConsumerState.CriticallyNeedsEnergy));
        }
        else if (e.Sensor.State == CompletedState)
        {
            CheckDesiredState(new EnergyConsumerStopCommand(this, EnergyConsumerState.Off));
        }
        else if (StatePeakLoads.Select(x => x.State).Contains(StateSensor.State))
        {
            CheckDesiredState(new EnergyConsumerStartCommand(this, EnergyConsumerState.Running));
        }
    }

    private void Socket_TurnedOn(object? sender, BinarySensorEventArgs e)
    {
        if (StateSensor.State != StartState && StatePeakLoads.Select(x => x.State).Contains(StateSensor.State))
            CheckDesiredState(new EnergyConsumerStartedEvent(this, EnergyConsumerState.Running));
    }

    private void Socket_TurnedOff(object? sender, BinarySensorEventArgs e)
    {
        CheckDesiredState(new EnergyConsumerStoppedEvent(this, EnergyConsumerState.Off));
    }

}