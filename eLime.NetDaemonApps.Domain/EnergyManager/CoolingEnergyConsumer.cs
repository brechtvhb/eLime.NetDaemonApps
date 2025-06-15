using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using Microsoft.Extensions.Logging;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.EnergyManager;

public class CoolingEnergyConsumer : EnergyConsumer
{
    public NumericEntity TemperatureSensor { get; set; }
    public double TargetTemperature { get; set; }
    public double MaxTemperature { get; set; }

    public override bool Running => Socket.IsOn();
    public BinarySwitch Socket { get; }
    public override double PeakLoad { get; }


    public CoolingEnergyConsumer(ILogger logger, string name, List<string> consumerGroups, NumericEntity powerUsage, BinarySensor? criticallyNeeded, double switchOnLoad, double switchOffLoad, TimeSpan? minimumRuntime, TimeSpan? maximumRuntime, TimeSpan? minimumTimeout,
        TimeSpan? maximumTimeout, List<TimeWindow> timeWindows, string timezone, BinarySwitch socket, double peakLoad, NumericEntity temperatureSensor, double targetTemperature, double maxTemperature)
    {
        SetCommonFields(logger, name, consumerGroups, powerUsage, criticallyNeeded, switchOnLoad, switchOffLoad, minimumRuntime, maximumRuntime, minimumTimeout, maximumTimeout, timeWindows, timezone);
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
        //Do not care about minimum runtime if peak load hits, happens only several times a month ...
        //if (MinimumRuntime != null && StartedAt?.Add(MinimumRuntime.Value) > now)
        //    return false;

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

    public override void DisposeInternal()
    {
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