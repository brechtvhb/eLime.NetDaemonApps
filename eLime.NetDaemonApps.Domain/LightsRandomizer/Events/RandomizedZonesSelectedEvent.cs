using MediatR;

namespace eLime.NetDaemonApps.Domain.LightsRandomizer.Events;

public class RandomizedZonesSelectedEvent(List<(String zone, String scene)> selectedZones) : INotification
{
    public List<(String zone, String scene)> SelectedZones => selectedZones;
}