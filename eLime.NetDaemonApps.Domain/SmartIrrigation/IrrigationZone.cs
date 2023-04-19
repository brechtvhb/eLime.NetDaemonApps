using eLime.NetDaemonApps.Domain.Entities.BinarySensors;

namespace eLime.NetDaemonApps.Domain.SmartIrrigation;

public abstract class IrrigationZone : IDisposable
{
    public String Name { get; private set; }
    public Int32 FlowRate { get; private set; }
    public BinarySwitch Valve { get; private set; }

    public Boolean CurrentlyWatering => Valve.IsOn();
    public DateTimeOffset? WateringStartedAt { get; protected set; }
    public DateTimeOffset? LastWatering { get; protected set; }

    public NeedsWatering State { get; private set; }

    public ZoneMode Mode { get; private set; }

    protected void SetCommonFields(String name, Int32 flowRate, BinarySwitch valve)
    {
        Name = name;
        FlowRate = flowRate;
        Valve = valve;
        Valve.TurnedOn += Valve_TurnedOn;
        Valve.TurnedOff += Valve_TurnedOff;
    }

    private void Valve_TurnedOn(object? sender, BinarySensorEventArgs e)
    {
        StateChanged?.Invoke(this, new IrrigationZoneWateringStartedEvent(this, State));
    }

    private void Valve_TurnedOff(object? sender, BinarySensorEventArgs e)
    {
        StateChanged?.Invoke(this, new IrrigationZoneWateringEndedEvent(this, State));
    }

    public void SetState(NeedsWatering state)
    {
        State = state;
    }

    public void SetMode(ZoneMode mode)
    {
        Mode = mode;
    }

    public void SetStartWateringDate(DateTimeOffset now)
    {
        WateringStartedAt = now;
        CheckDesiredState();
    }

    public void SetLastWateringDate(DateTimeOffset now)
    {
        WateringStartedAt = null;
        LastWatering = now;
        CheckDesiredState();
    }

    public event EventHandler<IrrigationZoneStateChangedEvent>? StateChanged;

    protected void OnStateCHanged(IrrigationZoneStateChangedEvent e)
    {
        StateChanged?.Invoke(this, e);
    }


    public void CheckDesiredState()
    {
        var desiredState = GetDesiredState();

        if (State == desiredState)
            return;

        State = desiredState;

        IrrigationZoneStateChangedEvent? @event = desiredState switch
        {
            NeedsWatering.Yes => new IrrigationZoneWateringNeededEvent(this, State),
            NeedsWatering.Critical => new IrrigationZoneWateringNeededEvent(this, State),
            NeedsWatering.No => new IrrigationZoneEndWateringEvent(this, State),
            NeedsWatering.Ongoing => null,
            NeedsWatering.Unknown => null,
            _ => null
        };

        if (@event != null)
            OnStateCHanged(@event);
    }

    protected abstract NeedsWatering GetDesiredState();
    public abstract bool CanStartWatering(DateTimeOffset now, bool energyAvailable);
    public abstract bool CheckForForceStop(DateTimeOffset now);

    protected TimeSpan? GetRemainingRrunTime(TimeSpan? suggestedRunTime, DateTimeOffset now)
    {
        var remainingDuration = now - WateringStartedAt;

        return remainingDuration < suggestedRunTime ? remainingDuration : suggestedRunTime;
    }

    public void Dispose()
    {
        Valve.TurnedOn -= Valve_TurnedOn;
        Valve.TurnedOn -= Valve_TurnedOff;
        Valve.Dispose();
    }
}