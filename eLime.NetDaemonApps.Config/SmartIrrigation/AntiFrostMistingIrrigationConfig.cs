namespace eLime.NetDaemonApps.Config.SmartIrrigation;

public class AntiFrostMistingIrrigationConfig
{
    public string TemperatureEntity { get; set; }
    public int CriticalTemperature { get; set; }
    public int LowTemperature { get; set; }
    public TimeSpan MistingDuration { get; set; }
    public TimeSpan MistingTimeout { get; set; }
}