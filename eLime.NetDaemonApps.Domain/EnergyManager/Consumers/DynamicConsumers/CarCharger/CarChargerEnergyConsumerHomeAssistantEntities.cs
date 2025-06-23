using eLime.NetDaemonApps.Domain.Entities.Input;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.Entities.TextSensors;

namespace eLime.NetDaemonApps.Domain.EnergyManager.Consumers.DynamicConsumers.CarCharger;

public class CarChargerEnergyConsumerHomeAssistantEntities(EnergyConsumerConfiguration config)
    : EnergyConsumerHomeAssistantEntities(config)
{
    internal InputNumberEntity CurrentNumber = config.CarCharger!.CurrentSensor;
    internal NumericSensor VoltageSensor = config.CarCharger!.VoltageSensor;
    internal TextSensor StateSensor = config.CarCharger!.StateSensor;
    public new void Dispose()
    {
        base.Dispose();
        CurrentNumber.Dispose();
        VoltageSensor.Dispose();
        StateSensor.Dispose();
    }
}