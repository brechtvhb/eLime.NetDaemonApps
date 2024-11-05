using eLime.NetDaemonApps.Domain.LightsRandomizer.Events;
using eLime.NetDaemonApps.Domain.Storage;
using MediatR;
using Microsoft.Extensions.Logging;

namespace eLime.NetDaemonApps.Domain.LightsRandomizer.Handlers;

public class RandomizedZonesSelectedEventHandler(ILogger<RandomizedZonesSelectedEventHandler> logger, IFileStorage fileStorage) : INotificationHandler<RandomizedZonesSelectedEvent>
{
    public Task Handle(RandomizedZonesSelectedEvent command, CancellationToken cancellationToken)
    {
        var storage = new LightsRandomizerStorage
        {
            SelectedZones = command.SelectedZones.Select(x => new SelectedZoneStorage { Zone = x.zone, Scene = x.scene }).ToList()
        };
        fileStorage.Save("LightsRandomizer", "LightsRandomizer", storage);

        logger.LogDebug("RandomizedZonesSelectedEventHandler: Today's random zones were selected. Surprise surprise.");

        return Task.CompletedTask;

        //Could also have sensor and publish selected lights to home assistant
    }
}