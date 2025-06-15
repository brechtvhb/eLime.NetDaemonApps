namespace eLime.NetDaemonApps.Config.EnergyManager;

public class TimeWindowConfig
{
    public string? ActiveEntity { get; set; }
    public TimeSpan Start { get; set; }
    public TimeSpan End { get; set; }
}