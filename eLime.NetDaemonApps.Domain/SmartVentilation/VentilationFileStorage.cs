namespace eLime.NetDaemonApps.Domain.SmartVentilation;

internal class VentilationFileStorage
{
    public bool Enabled { get; set; }
    public DateTimeOffset? LastStateChange { get; set; }
    public VentilationGuards? LastStateChangeTriggeredBy { get; set; }

    public bool Equals(VentilationFileStorage? r)
    {
        if (r == null)
            return false;

        return Enabled == r.Enabled && LastStateChange == r.LastStateChange && LastStateChangeTriggeredBy == r.LastStateChangeTriggeredBy;
    }
}