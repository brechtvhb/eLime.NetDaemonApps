namespace eLime.NetDaemonApps.Domain.SmartIrrigation;

public class IrrigationZoneStateChangedEvent : EventArgs
{
    public IrrigationZoneStateChangedEvent(IrrigationZone zone, NeedsWatering state)
    {
        Zone = zone;
        State = state;
    }

    public IrrigationZone Zone { get; init; }
    public NeedsWatering? State { get; init; }

}

public class IrrigationZoneEndWateringEvent : IrrigationZoneStateChangedEvent
{
    public IrrigationZoneEndWateringEvent(IrrigationZone zone, NeedsWatering state) : base(zone, state)
    {
    }
}
public class IrrigationZoneWateringStartedEvent : IrrigationZoneStateChangedEvent
{
    public IrrigationZoneWateringStartedEvent(IrrigationZone zone, NeedsWatering state) : base(zone, state)
    {
    }
}

public class IrrigationZoneWateringNeededEvent : IrrigationZoneStateChangedEvent
{
    public IrrigationZoneWateringNeededEvent(IrrigationZone zone, NeedsWatering state) : base(zone, state)
    {
    }
}