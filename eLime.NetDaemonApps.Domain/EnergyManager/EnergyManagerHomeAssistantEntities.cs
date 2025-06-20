using eLime.NetDaemonApps.Domain.Entities.NumericSensors;

namespace eLime.NetDaemonApps.Domain.EnergyManager;

public class EnergyManagerHomeAssistantEntities(EnergyManagerConfiguration config) : IDisposable
{
    internal NumericSensor SolarProductionRemainingTodaySensor = config.SolarProductionRemainingTodaySensor;

    public void Dispose()
    {
        SolarProductionRemainingTodaySensor.Dispose();
    }
}