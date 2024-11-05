using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Domain.LightsRandomizer.Events;
using eLime.NetDaemonApps.Domain.Mqtt;
using MediatR;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;

namespace eLime.NetDaemonApps.Domain.LightsRandomizer.Handlers;

public class InitLightsRandomizerZoneCommandHandler(ILogger<InitLightsRandomizerZoneCommandHandler> logger, IMqttEntityManager mqttEntityManager) : INotificationHandler<InitLightsRandomizerZoneCommand>
{
    public Device GetDevice() => new() { Identifiers = [$"light_randomizer"], Name = "Light randomizer", Manufacturer = "Me" };

    public async Task Handle(InitLightsRandomizerZoneCommand command, CancellationToken cancellationToken)
    {
        foreach (var scene in command.AllowedScenes)
        {
            await InitScene(command.Zone, scene);
        }
    }

    private async Task InitScene(String zone, String scene)
    {
        logger.LogDebug("InitLightsRandomizerZoneCommandHandler: Initialized Zone: {Zone} ({Scene})", zone, scene);
        var sensorName = $"binary_sensor.light_randomizer_{zone.MakeHaFriendly()}_{scene.MakeHaFriendly()}";

        var zoneAttributes = new EnabledSwitchAttributes { Icon = "hue:friends-of-hue-retrotouch-black-chrome", Device = GetDevice() };
        await mqttEntityManager.CreateAsync(sensorName, new EntityCreationOptions(UniqueId: sensorName, Name: $"Zone {zone} - {scene} selected", DeviceClass: "occupancy", Persist: true), zoneAttributes);
    }
}