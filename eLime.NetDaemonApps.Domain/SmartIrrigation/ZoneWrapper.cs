namespace eLime.NetDaemonApps.Domain.SmartIrrigation;

public class ZoneWrapper
{
    public IrrigationZone Zone { get; set; }
    public IDisposable? ModeChangedCommandHandler { get; set; }
    public IDisposable? EndWateringtimer { get; set; }

}