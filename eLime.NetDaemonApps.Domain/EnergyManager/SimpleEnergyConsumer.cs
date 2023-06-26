using eLime.NetDaemonApps.Domain.Entities.BinarySensors;

namespace eLime.NetDaemonApps.Domain.EnergyManager;

public class SimpleEnergyConsumer : EnergyConsumer
{
    public BinarySwitch Socket { get; set; }
    public override bool IsRunning()
    {
        return Socket.IsOn();
    }

    public TimeSpan? CriticalTimeout { get; }

    protected override EnergyConsumerState GetDesiredState(DateTimeOffset? now)
    {
        if (now == null && IsRunning())
        {
            return EnergyConsumerState.Running;
        }
        if (now == null && !IsRunning())
        {
            return EnergyConsumerState.Off;
        }

        if (!IsRunning())
        {
            if (CriticalTimeout != null && LastRun?.Add(CriticalTimeout.Value) < now)
                return EnergyConsumerState.CriticallyNeedsEnergy;

            if (MaximumTimeout != null && LastRun?.Add(MaximumTimeout.Value) < now)
                return EnergyConsumerState.NeedsEnergy;

            return EnergyConsumerState.RunOnSolarExcess;
        }

        if (MaximumRuntime != null && LastRun?.Add(MaximumRuntime.Value) < now)
            return EnergyConsumerState.Off;

        return EnergyConsumerState.Running;
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


    public override bool ShouldForceStop(DateTimeOffset now)
    {
        if (MaximumRuntime != null && StartedAt?.Add(MaximumRuntime.Value) < now)
            return true;

        if (!IsWithinTimeWindow(now) && HasTimeWindow())
            return true;

        if (State == EnergyConsumerState.Off & Socket.IsOn())
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