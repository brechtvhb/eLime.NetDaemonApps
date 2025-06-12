using eLime.NetDaemonApps.Domain.EnergyManager2.Configuration;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.Buttons;
using eLime.NetDaemonApps.Domain.Entities.TextSensors;

namespace eLime.NetDaemonApps.Domain.EnergyManager2.HomeAssistant;

public class TriggeredEnergyConsumerHomeAssistantEntities(EnergyConsumerConfiguration config)
    : EnergyConsumerHomeAssistantEntities(config)
{
    internal BinarySwitch? SocketSwitch = config.Triggered!.SocketSwitch;
    internal Button? StartButton = config.Triggered.StartButton;
    internal BinarySwitch? PauseSwitch = config.Triggered.PauseSwitch;
    internal TextSensor StateSensor = config.Triggered.StateSensor;
}