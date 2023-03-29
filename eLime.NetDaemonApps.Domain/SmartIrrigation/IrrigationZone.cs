using eLime.NetDaemonApps.Domain.Entities.BinarySensors;

namespace eLime.NetDaemonApps.Domain.SmartIrrigation;

public abstract class IrrigationZone : IDisposable
{
    public String Name { get; private set; }
    public Int32 FlowRate { get; private set; }
    public BinarySwitch Valve { get; private set; }

    public Boolean CurrentlyWatering { get; protected set; }
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
        WateringStarted();
    }

    private void Valve_TurnedOff(object? sender, BinarySensorEventArgs e)
    {
        WateringEnded();
    }

    public void SetMode(ZoneMode mode)
    {
        Mode = mode;
    }

    public void WateringStarted()
    {
        CurrentlyWatering = true;
        CheckDesiredState();
    }

    public void WateringEnded()
    {
        CurrentlyWatering = false;
        LastWatering = DateTime.Now;
        CheckDesiredState();
    }

    public event EventHandler<IrrigationZoneStateChangedEvent>? StateChanged;

    protected void OnStateCHanged(IrrigationZoneStateChangedEvent e)
    {
        StateChanged?.Invoke(this, e);
    }

    protected void CheckDesiredState()
    {
        var desiredState = GetDesiredState();

        if (State == desiredState)
            return;

        State = desiredState;

        IrrigationZoneStateChangedEvent @event = desiredState switch
        {
            NeedsWatering.Yes => new IrrigationZoneWateringNeededEvent(this, State),
            NeedsWatering.Critical => new IrrigationZoneWateringNeededEvent(this, State),
            NeedsWatering.Ongoing => new IrrigationZoneWateringStartedEvent(this, State),
            NeedsWatering.No => new IrrigationZoneEndWateringEvent(this, State),
            NeedsWatering.Unknown => throw new ArgumentOutOfRangeException(),
            _ => throw new ArgumentOutOfRangeException()
        };

        OnStateCHanged(@event);
    }

    protected abstract NeedsWatering GetDesiredState();
    public abstract bool CanStartWatering(DateTimeOffset now);

    public void Dispose()
    {
        Valve.TurnedOn -= Valve_TurnedOn;
        Valve.TurnedOn -= Valve_TurnedOff;
        Valve.Dispose();
    }
}