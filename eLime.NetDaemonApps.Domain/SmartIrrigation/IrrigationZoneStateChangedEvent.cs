namespace eLime.NetDaemonApps.Domain.SmartIrrigation;

public class IrrigationZoneStateChangedEvent : EventArgs
{
    public IrrigationZoneStateChangedEvent(IrrigationZone zone, NeedsWatering state)
    {
        Zone = zone;
        State = state;
    }

    public IrrigationZone Zone { get; set; }
    public NeedsWatering? State { get; set; }

}

public class IrrigationZoneWateringNeededEvent : IrrigationZoneStateChangedEvent
{
    public IrrigationZoneWateringNeededEvent(IrrigationZone zone, NeedsWatering state) : base(zone, state)
    {
    }
}

public class IrrigationZoneWateringStartedEvent : IrrigationZoneStateChangedEvent
{
    public IrrigationZoneWateringStartedEvent(IrrigationZone zone, NeedsWatering state) : base(zone, state)
    {
    }
}

public class IrrigationZoneEndWateringEvent : IrrigationZoneStateChangedEvent
{
    public IrrigationZoneEndWateringEvent(IrrigationZone zone, NeedsWatering state) : base(zone, state)
    {
    }
}

public class IrrigationZoneWateringEndedEvent : IrrigationZoneStateChangedEvent
{
    public IrrigationZoneWateringEndedEvent(IrrigationZone zone, NeedsWatering state) : base(zone, state)
    {
    }
}