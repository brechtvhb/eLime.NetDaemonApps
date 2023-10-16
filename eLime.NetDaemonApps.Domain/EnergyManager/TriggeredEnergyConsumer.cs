using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.TextSensors;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.EnergyManager;

public class TriggeredEnergyConsumer : EnergyConsumer
{
    public BinarySwitch Socket { get; }

    public override bool Running => Socket.IsOn();


    public TextSensor StateSensor { get; }
    public String StartState { get; }
    public String CompletedState { get; }
    public String? CriticalState { get; }
    public Boolean CanForceShutdown { get; }
    public Boolean ShutDownOnComplete { get; }

    public List<(String State, Double PeakLoad)> StatePeakLoads { get; }

    public override double PeakLoad
    {
        get
        {
            var currentState = StateSensor.State;
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

    public TriggeredEnergyConsumer(String name, NumericEntity powerUsage, BinarySensor? criticallyNeeded, Double switchOnLoad, Double switchOffLoad, TimeSpan? minimumRuntime, TimeSpan? maximumRuntime, TimeSpan? minimumTimeout,
        TimeSpan? maximumTimeout, List<TimeWindow> timeWindows, BinarySwitch socket, List<(String State, Double PeakLoad)> peakLoads, TextSensor stateSensor, String startState, String completedState, String criticalState, bool canForceShutdown, bool shutDownOnComplete)
    {
        SetCommonFields(name, powerUsage, criticallyNeeded, switchOnLoad, switchOffLoad, minimumRuntime, maximumRuntime, minimumTimeout, maximumTimeout, timeWindows);
        Socket = socket;
        Socket.TurnedOn += Socket_TurnedOn;
        Socket.TurnedOff += Socket_TurnedOff;

        StateSensor = stateSensor;
        StateSensor.StateChanged += StateSensor_StateChanged;

        StartState = startState;
        CompletedState = completedState;
        CriticalState = criticalState;

        StatePeakLoads = peakLoads;
        CanForceShutdown = canForceShutdown;
        ShutDownOnComplete = shutDownOnComplete;
    }

    protected override EnergyConsumerState GetDesiredState(DateTimeOffset? now)
    {
        return Running switch
        {
            true when StateSensor.State == CompletedState => EnergyConsumerState.Off,
            true => EnergyConsumerState.Running,
            false when StateSensor.State == StartState && CriticallyNeeded != null && CriticallyNeeded.IsOn() => EnergyConsumerState.CriticallyNeedsEnergy,
            false when StateSensor.State == CriticalState && !String.IsNullOrWhiteSpace(CriticalState) => EnergyConsumerState.CriticallyNeedsEnergy,
            false when StateSensor.State == StartState => EnergyConsumerState.NeedsEnergy,
            false => EnergyConsumerState.Off,
        };
    }


    public override bool CanStart(DateTimeOffset now)
    {
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

        if (!CanForceShutdown)
            return false;

        return true;
    }

    public override bool CanForceStopOnPeakLoad(DateTimeOffset now)
    {
        if (MinimumRuntime != null && StartedAt?.Add(MinimumRuntime.Value) > now)
            return false;

        if (!CanForceShutdown)
            return false;

        return true;
    }

    public override void TurnOn()
    {
        Socket.TurnOn();
    }

    public override void TurnOff()
    {
        if (CanForceShutdown || (ShutDownOnComplete && StateSensor.State == CompletedState))
            Socket.TurnOff();
    }

    public new void Dispose()
    {
        base.Dispose();

        Socket.TurnedOn -= Socket_TurnedOn;
        Socket.TurnedOn -= Socket_TurnedOff;
        Socket.Dispose();

        StateSensor.StateChanged -= StateSensor_StateChanged;
        StateSensor.Dispose();
    }

    private void StateSensor_StateChanged(object? sender, TextSensorEventArgs e)
    {
        if (e.Sensor.State == StartState)
            CheckDesiredState(new EnergyConsumerStartCommand(this, EnergyConsumerState.NeedsEnergy));

        if (e.Sensor.State == CompletedState)
            CheckDesiredState(new EnergyConsumerStopCommand(this, EnergyConsumerState.Off));

        if (e.Sensor.State == CriticalState)
            CheckDesiredState(new EnergyConsumerStartCommand(this, EnergyConsumerState.CriticallyNeedsEnergy));

    }

    private void Socket_TurnedOn(object? sender, BinarySensorEventArgs e)
    {
        CheckDesiredState(new EnergyConsumerStartedEvent(this, EnergyConsumerState.Running));
    }

    private void Socket_TurnedOff(object? sender, BinarySensorEventArgs e)
    {
        CheckDesiredState(new EnergyConsumerStoppedEvent(this, EnergyConsumerState.Off));
    }

}