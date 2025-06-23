using MediatR;

namespace eLime.NetDaemonApps.Domain.LightsRandomizer.Events;

public class RandomizedZonesSelectedEvent(List<(string zone, string scene)> selectedZones) : INotification
{
    public List<(string zone, string scene)> SelectedZones => selectedZones;
}