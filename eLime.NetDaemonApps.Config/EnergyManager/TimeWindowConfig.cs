namespace eLime.NetDaemonApps.Config.EnergyManager;

public class TimeWindowConfig
{
    public string ActiveEntity { get; set; }
    public TimeSpan Start { get; }
    public TimeSpan End { get; }
}