using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.EnergyManager;

public class CoolingEnergyConsumer : EnergyConsumer
{
    public NumericEntity TemperatureSensor { get; set; }
    public Double TargetTemperature { get; set; }
    public Double MaxTemperature { get; set; }

    public override bool Running => Socket.IsOn();
    public BinarySwitch Socket { get; }
    public override double PeakLoad { get; }


    public CoolingEnergyConsumer(String name, NumericEntity powerUsage, BinarySensor? criticallyNeeded, Double switchOnLoad, Double switchOffLoad, TimeSpan? minimumRuntime, TimeSpan? maximumRuntime, TimeSpan? minimumTimeout,
        TimeSpan? maximumTimeout, List<TimeWindow> timeWindows, BinarySwitch socket, Double peakLoad, NumericEntity temperatureSensor, Double targetTemperature, Double maxTemperature)
    {
        SetCommonFields(name, powerUsage, criticallyNeeded, switchOnLoad, switchOffLoad, minimumRuntime, maximumRuntime, minimumTimeout, maximumTimeout, timeWindows);
        Socket = socket;
        Socket.TurnedOn += Socket_TurnedOn;
        Socket.TurnedOff += Socket_TurnedOff;

        TemperatureSensor = temperatureSensor;
        TargetTemperature = targetTemperature;
        MaxTemperature = maxTemperature;

        PeakLoad = peakLoad;
    }

    protected override EnergyConsumerState GetDesiredState(DateTimeOffset? now)
    {
        return Running switch
        {
            true when TemperatureSensor.State < TargetTemperature => EnergyConsumerState.Off,
            true => EnergyConsumerState.Running,
            false when MaximumTimeout != null && LastRun?.Add(MaximumTimeout.Value) < now => EnergyConsumerState.CriticallyNeedsEnergy,
            false when CriticallyNeeded != null && CriticallyNeeded.IsOn() => EnergyConsumerState.CriticallyNeedsEnergy,
            false when TemperatureSensor.State >= MaxTemperature => EnergyConsumerState.CriticallyNeedsEnergy,
            false when TemperatureSensor.State >= TargetTemperature => EnergyConsumerState.NeedsEnergy,
            false => EnergyConsumerState.Off
        };
    }

    public override bool CanStart(DateTimeOffset now)
    {
        if (State is EnergyConsumerState.Running or EnergyConsumerState.Off)
            return false;

        if (!IsWithinTimeWindow(now) && HasTimeWindow())
            return false;

        if (TemperatureSensor.State < TargetTemperature)
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

        if (TemperatureSensor.State > MaxTemperature)
            return false;

        return true;
    }

    public override bool CanForceStopOnPeakLoad(DateTimeOffset now)
    {
        if (MinimumRuntime != null && StartedAt?.Add(MinimumRuntime.Value) > now)
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