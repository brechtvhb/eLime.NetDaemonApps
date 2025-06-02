using eLime.NetDaemonApps.Domain.EnergyManager2.Configuration;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;

namespace eLime.NetDaemonApps.Domain.EnergyManager2.HomeAssistant;

public class EnergyManagerHomeAssistantEntities(EnergyManagerConfiguration config) : IDisposable
{
    internal NumericSensor SolarProductionRemainingTodaySensor = config.SolarProductionRemainingTodaySensor;

    public void Dispose()
    {
        SolarProductionRemainingTodaySensor.Dispose();
    }
}