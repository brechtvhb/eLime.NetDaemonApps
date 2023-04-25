namespace eLime.NetDaemonApps.Config.SmartIrrigation;

public class AntiFrostMistingIrrigationConfig
{
    public string TemperatureEntity { get; set; }
    public double CriticalTemperature { get; set; }
    public double LowTemperature { get; set; }
    public TimeSpan MistingDuration { get; set; }
    public TimeSpan MistingTimeout { get; set; }
}