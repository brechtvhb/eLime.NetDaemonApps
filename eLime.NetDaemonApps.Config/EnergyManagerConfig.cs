using eLime.NetDaemonApps.Config.EnergyManager;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace eLime.NetDaemonApps.Config;

public class EnergyManagerConfig
{
    public string Timezone { get; set; }
    public GridConfig Grid { get; set; }
    public string SolarProductionRemainingTodayEntity { get; set; }
    public string PhoneToNotify { get; set; }
    public List<EnergyConsumerConfig> Consumers { get; set; }
    public BatteryManagerConfig BatteryManager { get; set; }

}