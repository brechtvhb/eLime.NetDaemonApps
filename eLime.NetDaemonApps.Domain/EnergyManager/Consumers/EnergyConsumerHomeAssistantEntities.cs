using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;

namespace eLime.NetDaemonApps.Domain.EnergyManager.Consumers;

public class EnergyConsumerHomeAssistantEntities(EnergyConsumerConfiguration config) : IDisposable
{
    internal NumericSensor PowerConsumptionSensor = config.PowerConsumptionSensor;
    internal BinarySensor? CriticallyNeededSensor = config.CriticallyNeededSensor;

    public void Dispose()
    {
        PowerConsumptionSensor.Dispose();
        CriticallyNeededSensor?.Dispose();
    }
}