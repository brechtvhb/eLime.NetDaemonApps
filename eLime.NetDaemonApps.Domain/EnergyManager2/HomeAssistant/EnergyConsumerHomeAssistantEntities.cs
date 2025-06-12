using eLime.NetDaemonApps.Domain.EnergyManager2.Configuration;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;

namespace eLime.NetDaemonApps.Domain.EnergyManager2.HomeAssistant;

public class EnergyConsumerHomeAssistantEntities(EnergyConsumerConfiguration config) : IDisposable
{
    internal NumericSensor PowerUsageSensor = config.PowerUsageSensor;
    internal BinarySensor? CriticallyNeededSensor = config.CriticallyNeededSensor;

    public void Dispose()
    {
        PowerUsageSensor.Dispose();
        CriticallyNeededSensor?.Dispose();
    }
}