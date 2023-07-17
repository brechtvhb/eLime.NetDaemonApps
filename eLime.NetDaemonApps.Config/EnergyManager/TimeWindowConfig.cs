namespace eLime.NetDaemonApps.Config.EnergyManager;

public class TimeWindowConfig
{
    public string IsActiveEntity { get; set; }
    public TimeOnly Start { get; }
    public TimeOnly End { get; }
}