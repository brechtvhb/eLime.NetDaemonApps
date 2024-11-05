using eLime.NetDaemonApps.Domain.Extensions;
using eLime.NetDaemonApps.Domain.LightsRandomizer.Events;
using MediatR;

namespace eLime.NetDaemonApps.Domain.LightsRandomizer;

public class LightRandomizer
{
    public List<LightingZone> Zones { get; private init; } = [];
    public int AmountOfZonesToLight { get; private init; }

    public List<(String zone, String scene)> SelectedZones { get; private set; } = [];

    public static LightRandomizer Create(IMediator mediator, List<LightingZone> zones, int amountOfZonesToLight, LightsRandomizerStorage storage)
    {
        var lightRandomizer = new LightRandomizer
        {
            Zones = zones,
            AmountOfZonesToLight = amountOfZonesToLight,
            SelectedZones = storage?.SelectedZones.Select(x => (x.Zone, x.Scene)).ToList() ?? []
        };
        List<INotification> domainEvents = [];

        domainEvents.AddRange(lightRandomizer.Zones.Select(x => new InitLightsRandomizerZoneCommand(x.Name, x.AllowedScenes)).ToList());
        foreach (var domainEvent in domainEvents)
            mediator.Publish(domainEvent);

        return lightRandomizer;
    }

    public void SelectZonesForToday(IMediator mediator)
    {
        List<(string zone, string scene)> selectedZones = [];

        var zones = Zones.GetRandomItems(AmountOfZonesToLight);

        foreach (var zone in zones)
        {
            var scene = zone.AllowedScenes.GetRandomItem();
            selectedZones.Add((zone.Name, scene));
        }

        SelectedZones = selectedZones;

        mediator.Publish(new RandomizedZonesSelectedEvent(SelectedZones));
    }

    public void TurnOnLights(IMediator mediator)
    {
        foreach (var (zone, scene) in SelectedZones)
            mediator.Publish(new TurnOnZoneLightsCommand(zone, scene));
    }
    public void TurnOffLights(IMediator mediator)
    {
        foreach (var (zone, scene) in SelectedZones)
            mediator.Publish(new TurnOffZoneLightsCommand(zone, scene));
    }
}