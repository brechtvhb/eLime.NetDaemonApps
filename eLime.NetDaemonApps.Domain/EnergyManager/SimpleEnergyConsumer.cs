using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.EnergyManager;

public class SimpleEnergyConsumer : EnergyConsumer
{
    public BinarySwitch Socket { get; }

    public override bool Running => Socket.IsOn();
    public override double PeakLoad { get; }


    public SimpleEnergyConsumer(String name, NumericEntity powerUsage, BinarySensor criticallyNeeded, Double switchOnLoad, TimeSpan? minimumRuntime, TimeSpan? maximumRuntime, TimeSpan? minimumTimeout,
        TimeSpan? maximumTimeout, List<TimeWindow> timeWindows, BinarySwitch socket, Double peakLoad)
    {
        SetCommonFields(name, powerUsage, criticallyNeeded, switchOnLoad, minimumRuntime, maximumRuntime, minimumTimeout, maximumTimeout, timeWindows);
        Socket = socket;
        Socket.TurnedOn += Socket_TurnedOn;
        Socket.TurnedOff += Socket_TurnedOff;

        PeakLoad = peakLoad;
    }
    protected override EnergyConsumerState GetDesiredState(DateTimeOffset? now)
    {
        return Running switch
        {
            true => EnergyConsumerState.Running,
            false when MaximumTimeout != null && LastRun?.Add(MaximumTimeout.Value) < now => EnergyConsumerState.CriticallyNeedsEnergy,
            false when CriticallyNeeded.IsOn() && MinimumTimeout != null && LastRun?.Add(MinimumTimeout.Value) < now => EnergyConsumerState.CriticallyNeedsEnergy,
            false when MinimumTimeout != null && LastRun?.Add(MinimumTimeout.Value) < now => EnergyConsumerState.NeedsEnergy,
            false when MinimumTimeout == null && MaximumTimeout == null => EnergyConsumerState.NeedsEnergy,
            false => EnergyConsumerState.Off
        };
    }

    public override bool CanStart(DateTimeOffset now)
    {
        if (State is EnergyConsumerState.Running or EnergyConsumerState.Off)
            return false;

        if (!IsWithinTimeWindow(now) && HasTimeWindow())
            return false;

        if (MinimumTimeout == null || LastRun == null)
            return true;

        return !(LastRun?.Add(MinimumTimeout.Value) > now);
    }


    public override bool CanForceStop(DateTimeOffset now)
    {
        if (MinimumRuntime != null && StartedAt?.Add(MinimumRuntime.Value) < now)
            return true;

        if (!IsWithinTimeWindow(now) && HasTimeWindow())
            return true;

        return false;
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