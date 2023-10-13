namespace eLime.NetDaemonApps.Domain.SmartIrrigation;

internal class IrrigationZoneFileStorage
{
    public NeedsWatering State { get; set; }
    public ZoneMode Mode { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? LastRun { get; set; }
}