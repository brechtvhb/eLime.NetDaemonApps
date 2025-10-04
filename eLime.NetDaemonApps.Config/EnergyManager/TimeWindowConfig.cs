namespace eLime.NetDaemonApps.Config.EnergyManager;

public class TimeWindowConfig
{
    public string? ActiveSensor { get; set; }
    public List<DayOfWeek> Days { get; set; } = [];
    public TimeSpan Start { get; set; }
    public TimeSpan End { get; set; }
}