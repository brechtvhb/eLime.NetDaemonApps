using eLime.NetDaemonApps.Domain.Entities.NumericSensors;

namespace eLime.NetDaemonApps.Domain.EnergyManager.BatteryManager;

public class BatteryManagerHomeAssistantEntities(BatteryManagerConfiguration config)
{
    internal NumericSensor TotalChargePowerSensor = config.TotalChargePowerSensor;
    internal NumericSensor TotalDischargePowerSensor = config.TotalDischargePowerSensor;

    public void Dispose()
    {
    }
}