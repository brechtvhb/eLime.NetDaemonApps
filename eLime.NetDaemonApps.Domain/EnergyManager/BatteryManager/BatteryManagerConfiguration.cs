using eLime.NetDaemonApps.Config.EnergyManager;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using NetDaemon.HassModel;

namespace eLime.NetDaemonApps.Domain.EnergyManager.BatteryManager;

public class BatteryManagerConfiguration
{
    public BatteryManagerConfiguration(IHaContext haContext, BatteryManagerConfig config)
    {
        TotalChargePowerSensor = NumericSensor.Create(haContext, config.TotalChargePowerSensor);
        TotalDischargePowerSensor = NumericSensor.Create(haContext, config.TotalDischargePowerSensor);
        Batteries = config.Batteries?.Select(b => new BatteryConfiguration(haContext, b)).ToList() ?? [];
    }
    public NumericSensor TotalChargePowerSensor { get; set; }
    public NumericSensor TotalDischargePowerSensor { get; set; }
    public List<BatteryConfiguration> Batteries { get; set; }
}