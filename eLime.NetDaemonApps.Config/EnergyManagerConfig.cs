using eLime.NetDaemonApps.Config.EnergyManager;

namespace eLime.NetDaemonApps.Config;

public class EnergyManagerConfig
{
    public GridConfig Grid { get; set; }
    public String SolarProductionRemainingTodayEntity { get; set; }
    public String PhoneToNotify { get; set; }
    public List<EnergyConsumerConfig> Consumers { get; set; }
}