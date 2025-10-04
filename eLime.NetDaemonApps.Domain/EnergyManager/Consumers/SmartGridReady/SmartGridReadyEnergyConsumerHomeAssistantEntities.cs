using eLime.NetDaemonApps.Domain.Entities.Select;
using eLime.NetDaemonApps.Domain.Entities.TextSensors;

namespace eLime.NetDaemonApps.Domain.EnergyManager.Consumers.SmartGridReady;

public class SmartGridReadyEnergyConsumerHomeAssistantEntities(EnergyConsumerConfiguration config)
    : EnergyConsumerHomeAssistantEntities(config)
{
    internal SelectEntity SmartGridModeSelect = config.SmartGridReady!.SmartGridModeSelect;
    internal TextSensor StateSensor = config.SmartGridReady!.StateSensor;

    public new void Dispose()
    {
        base.Dispose();
        SmartGridModeSelect.Dispose();
        StateSensor.Dispose();
    }
}