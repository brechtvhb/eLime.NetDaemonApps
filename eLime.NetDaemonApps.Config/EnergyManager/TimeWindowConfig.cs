namespace eLime.NetDaemonApps.Config.EnergyManager;

public class TimeWindowConfig
{
    public string ActiveEntity { get; set; }
    public TimeOnly Start { get; }
    public TimeOnly End { get; }
}