using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Domain.Mqtt;
using MediatR;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;

namespace eLime.NetDaemonApps.Domain.LightsRandomizer.Handlers;

public class TurnOnZoneLightsCommandHandler(ILogger<TurnOnZoneLightsCommandHandler> logger, IMqttEntityManager mqttEntityManager) : INotificationHandler<TurnOnZoneLightsCommand>
{
    public Device GetDevice() => new() { Identifiers = [$"light_randomizer"], Name = "Light randomizer", Manufacturer = "Me" };

    public async Task Handle(TurnOnZoneLightsCommand command, CancellationToken cancellationToken)
    {
        var sensorName = $"binary_sensor.light_randomizer_{command.Zone.MakeHaFriendly()}_{command.Scene.MakeHaFriendly()}";
        await mqttEntityManager.SetStateAsync(sensorName, "ON");

        logger.LogDebug("TurnOnZoneLightsCommandHandler: Automated turn on for zone {Zone} ({Scene}).", command.Zone, command.Scene);
    }

}