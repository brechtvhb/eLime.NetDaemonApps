using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.Buttons;
using eLime.NetDaemonApps.Domain.Entities.TextSensors;

namespace eLime.NetDaemonApps.Domain.EnergyManager.Consumers.Triggered;

public class TriggeredEnergyConsumerHomeAssistantEntities(EnergyConsumerConfiguration config)
    : EnergyConsumerHomeAssistantEntities(config)
{
    internal BinarySwitch? SocketSwitch = config.Triggered!.SocketSwitch;
    internal Button? StartButton = config.Triggered.StartButton;
    internal Button? PauseButton = config.Triggered.PauseButton;
    internal TextSensor StateSensor = config.Triggered.StateSensor;
}