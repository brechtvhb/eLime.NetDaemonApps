using eLime.NetDaemonApps.Config.EnergyManager;
using eLime.NetDaemonApps.Domain.Entities.Select;
using eLime.NetDaemonApps.Domain.Entities.TextSensors;
using NetDaemon.HassModel;

namespace eLime.NetDaemonApps.Domain.EnergyManager.Consumers.SmartGridReady;

public class SmartGridReadyEnergyConsumerConfiguration
{
    public SmartGridReadyEnergyConsumerConfiguration(IHaContext haContext, SmartGridReadyEnergyConsumerConfig config)
    {
        SmartGridModeSelect = SelectEntity.Create(haContext, config.SmartGridModeEntity);
        StateSensor = TextSensor.Create(haContext, config.StateSensor);
        CanUseExcessEnergyState = config.CanUseExcessEnergyState;
        EnergyNeededState = config.EnergyNeededState;
        CriticalEnergyNeededState = config.CriticalEnergyNeededState;
        PeakLoad = config.PeakLoad;
        BlockedTimeWindows = config.BlockedTimeWindows.Select(x => new TimeWindowConfiguration(haContext, x)).ToList();
    }
    public SelectEntity SmartGridModeSelect { get; }
    public TextSensor StateSensor { get; }
    public string CanUseExcessEnergyState { get; }
    public string EnergyNeededState { get; }
    public string CriticalEnergyNeededState { get; }
    public double PeakLoad { get; }

    public List<TimeWindowConfiguration> BlockedTimeWindows { get; }
}