using eLime.NetDaemonApps.Domain.Entities.Input;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.EnergyManager.BatteryManager;

public class BatteryHomeAssistantEntities(BatteryConfiguration config)
{
    internal NumericEntity PowerSensor = config.PowerSensor;
    internal NumericSensor StateOfChargeSensor = config.StateOfChargeSensor;
    internal NumericEntity TotalEnergyChargedSensor = config.TotalEnergyChargedSensor;
    internal NumericEntity TotalEnergyDischargedSensor = config.TotalEnergyDischargedSensor;
    internal InputNumberEntity MaxChargePowerNumber = config.MaxChargePowerNumber;
    internal InputNumberEntity MaxDischargePowerNumber = config.MaxDischargePowerNumber;

    public void Dispose()
    {
    }
}