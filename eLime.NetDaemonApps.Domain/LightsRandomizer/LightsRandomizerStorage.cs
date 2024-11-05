
namespace eLime.NetDaemonApps.Domain.LightsRandomizer;

public class LightsRandomizerStorage
{
    public List<SelectedZoneStorage> SelectedZones { get; set; } = [];

    public bool Equals(LightsRandomizerStorage? r)
    {
        if (r == null)
            return false;

        return SelectedZones.SequenceEqual(r.SelectedZones);
    }
}

public class SelectedZoneStorage
{
    public String Zone { get; set; }
    public String Scene { get; set; }

    public bool Equals(SelectedZoneStorage? r)
    {
        if (r == null) return false;

        return Zone == r.Zone && Scene == r.Scene;
    }
}