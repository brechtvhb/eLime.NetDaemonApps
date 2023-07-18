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
    public String? CriticalState { get; }
    public Boolean CanForceShutdown { get; }

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

    public TriggeredEnergyConsumer(String name, NumericEntity powerUsage, BinarySensor? criticallyNeeded, Boolean preferSolar, Double switchOnLoad, TimeSpan? minimumRuntime, TimeSpan? maximumRuntime, TimeSpan? minimumTimeout,
        TimeSpan? maximumTimeout, List<TimeWindow> timeWindows, BinarySwitch socket, List<(String State, Double PeakLoad)> peakLoads, TextSensor stateSensor, String startState, String criticalState, bool canForceShutdown)
    {
        SetCommonFields(name, powerUsage, criticallyNeeded, preferSolar, switchOnLoad, minimumRuntime, maximumRuntime, minimumTimeout, maximumTimeout, timeWindows);
        Socket = socket;
        Socket.TurnedOn += Socket_TurnedOn;
        Socket.TurnedOff += Socket_TurnedOff;

        StateSensor = stateSensor;
        StartState = startState;
        CriticalState = criticalState;

        StatePeakLoads = peakLoads;
        CanForceShutdown = canForceShutdown;
    }

    protected override EnergyConsumerState GetDesiredState(DateTimeOffset? now)
    {
        return Running switch
        {
            true => EnergyConsumerState.Running,
            false when StateSensor.State == StartState => EnergyConsumerState.NeedsEnergy,
            false when StateSensor.State == CriticalState => EnergyConsumerState.CriticallyNeedsEnergy,
            false when StateSensor.State == StartState && CriticallyNeeded != null && CriticallyNeeded.IsOn() => EnergyConsumerState.CriticallyNeedsEnergy,
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
        Socket.TurnOff();
    }

    public new void Dispose()
    {
        base.Dispose();

        Socket.TurnedOn -= Socket_TurnedOn;
        Socket.TurnedOn -= Socket_TurnedOff;
        Socket.Dispose();
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