using eLime.NetDaemonApps.Domain.EnergyManager2.Configuration;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;

namespace eLime.NetDaemonApps.Domain.EnergyManager2.HomeAssistant;

public class SimpleEnergyConsumerHomeAssistantEntities(EnergyConsumerConfiguration config)
    : EnergyConsumerHomeAssistantEntities(config)
{
    internal BinarySwitch SocketSwitch = config.Simple!.SocketSwitch;
}