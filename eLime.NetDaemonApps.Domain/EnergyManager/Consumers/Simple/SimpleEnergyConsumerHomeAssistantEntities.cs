using eLime.NetDaemonApps.Domain.Entities.BinarySensors;

namespace eLime.NetDaemonApps.Domain.EnergyManager.Consumers.Simple;

public class SimpleEnergyConsumerHomeAssistantEntities(EnergyConsumerConfiguration config)
    : EnergyConsumerHomeAssistantEntities(config)
{
    internal BinarySwitch SocketSwitch = config.Simple!.SocketSwitch;

    public new void Dispose()
    {
        base.Dispose();
        SocketSwitch.Dispose();
    }
}