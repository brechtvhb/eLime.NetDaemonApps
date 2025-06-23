namespace eLime.NetDaemonApps.Config.FlexiScreens;

public class FlexiScreenConfig
{
    public string Name { get; set; }
    public bool? Enabled { get; set; }

    public string ScreenEntity { get; set; }
    public int Orientation { get; set; }

    public SunProtectionConfig SunProtection { get; set; }
    public StormProtectionConfig? StormProtection { get; set; }
    public TemperatureProtectionConfig? TemperatureProtection { get; set; }

    public string? SleepSensor { get; set; } //Eg: if ((workday && (time>19:15 or time<7:15)) or (!workday && (time>19:15 or time<8:45))) ?? Kids sleeping?
    public TimeSpan? MinimumIntervalSinceLastAutomatedAction { get; set; }
    public TimeSpan? MinimumIntervalSinceLastManualAction { get; set; }

}

public enum ScreenAction
{
    None,
    Up,
    Down
}