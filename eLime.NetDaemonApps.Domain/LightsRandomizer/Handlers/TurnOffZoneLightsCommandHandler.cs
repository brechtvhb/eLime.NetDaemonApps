using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Domain.Mqtt;
using MediatR;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;

namespace eLime.NetDaemonApps.Domain.LightsRandomizer.Handlers;

public class TurnOffZoneLightsCommandHandler(ILogger<TurnOffZoneLightsCommandHandler> logger, IMqttEntityManager mqttEntityManager) : INotificationHandler<TurnOffZoneLightsCommand>
{
    public Device GetDevice() => new() { Identifiers = [$"light_randomizer"], Name = "Light randomizer", Manufacturer = "Me" };

    public async Task Handle(TurnOffZoneLightsCommand command, CancellationToken cancellationToken)
    {
        var sensorName = $"binary_sensor.light_randomizer_{command.Zone.MakeHaFriendly()}_{command.Scene.MakeHaFriendly()}";
        await mqttEntityManager.SetStateAsync(sensorName, "OFF");

        logger.LogDebug("TurnOffZoneLightsCommandHandler: Automated turn off of zone {Zone} ({Scene}).", command.Zone, command.Scene);
    }

}