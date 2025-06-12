using eLime.NetDaemonApps.Domain.EnergyManager2.Configuration;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;

namespace eLime.NetDaemonApps.Domain.EnergyManager2.HomeAssistant;

public class CoolingEnergyConsumerHomeAssistantEntities(EnergyConsumerConfiguration config)
    : EnergyConsumerHomeAssistantEntities(config)
{
    internal BinarySwitch SocketSwitch = config.Cooling!.SocketSwitch;
    internal NumericSensor TemperatureSensor = config.Cooling!.TemperatureSensor;

    public new void Dispose()
    {
        base.Dispose();
        SocketSwitch.Dispose();
        TemperatureSensor.Dispose();
    }
}