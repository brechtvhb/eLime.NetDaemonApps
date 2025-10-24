using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.Entities.Select;
using eLime.NetDaemonApps.Domain.Entities.TextSensors;
using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Domain.SmartHeatPump;

namespace eLime.NetDaemonApps.Domain.EnergyManager.Consumers.DynamicConsumers.SmartGridReady;

public class SmartGridReadyEnergyConsumerHomeAssistantEntities(EnergyConsumerConfiguration config)
    : EnergyConsumerHomeAssistantEntities(config)
{
    internal SelectEntity SmartGridModeSelect = config.SmartGridReady!.SmartGridModeSelect;
    internal TextSensor StateSensor = config.SmartGridReady!.StateSensor;
    internal NumericSensor ExpectedPeakLoadSensor = config.SmartGridReady!.ExpectedPeakLoadSensor;

    internal SmartGridReadyMode GetSmartGridReadyMode()
    {
        return SmartGridModeSelect.State != null
            ? Enum<SmartGridReadyMode>.Cast(SmartGridModeSelect.State)
            : SmartGridReadyMode.Normal;
    }

    public new void Dispose()
    {
        base.Dispose();
        SmartGridModeSelect.Dispose();
        StateSensor.Dispose();
    }
}