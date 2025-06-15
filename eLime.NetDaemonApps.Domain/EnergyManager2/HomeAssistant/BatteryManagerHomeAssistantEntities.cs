using eLime.NetDaemonApps.Domain.EnergyManager2.Configuration;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;

namespace eLime.NetDaemonApps.Domain.EnergyManager2.HomeAssistant;

public class BatteryManagerHomeAssistantEntities(BatteryManagerConfiguration config)
{
    internal NumericSensor TotalChargePowerSensor = config.TotalChargePowerSensor;
    internal NumericSensor TotalDischargePowerSensor = config.TotalDischargePowerSensor;

    public void Dispose()
    {
    }
}