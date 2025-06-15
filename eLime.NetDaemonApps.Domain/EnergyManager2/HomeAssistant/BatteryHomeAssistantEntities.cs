using eLime.NetDaemonApps.Domain.EnergyManager2.Configuration;
using eLime.NetDaemonApps.Domain.Entities.Input;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.EnergyManager2.HomeAssistant;

public class BatteryHomeAssistantEntities(BatteryConfiguration config)
{
    internal NumericEntity PowerSensor = config.PowerSensor;
    internal NumericEntity StateOfChargeSensor = config.StateOfChargeSensor;
    internal NumericEntity TotalEnergyChargedSensor = config.TotalEnergyChargedSensor;
    internal NumericEntity TotalEnergyDischargedSensor = config.TotalEnergyDischargedSensor;
    internal InputNumberEntity MaxChargePowerNumber = config.MaxChargePowerNumber;
    internal InputNumberEntity MaxDischargePowerNumber = config.MaxDischargePowerNumber;

    public void Dispose()
    {
    }
}