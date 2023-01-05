namespace eLime.NetDaemonApps.Config.FlexiScreens;

public class FlexiScreenConfig
{
    public string Name { get; set; }
    public string ScreenEntity { get; set; }
    public int Orientation { get; set; }

    public string SunEntity { get; set; }
    public int OrientationStartThreshold { get; set; } // Prevent screens going up in sleeping rooms (on east side) to early.
    public int OrientationEndThreshold { get; set; }
    public decimal? ElevationThreshold { get; set; }
    public ScreenAction ActionToExecuteOnBelowElevation { get; set; } //For Living etc: Screen should go up, For sleeping rooms screen should go down

    public String SleepEntity { get; set; } //Eg: if ((workday && (time>19:15 or time<7:15)) or (!workday && (time>19:15 or time<8:45))) ?? Kids sleeping?

    public string SolarLuxEntity { get; set; }
    public int SolarLuxAboveThreshold { get; set; }
    public TimeSpan SolarLuxAboveDuration { get; set; }
    public int SolarLuxBelowThreshold { get; set; }
    public TimeSpan SolarLuxBelowDuration { get; set; }

    public string WindSpeedEntity { get; set; }
    public decimal? WindSpeedThreshold { get; set; }
    public TimeSpan? WindSpeedThresholdOnDuration { get; set; }
    public TimeSpan? WindSpeedThresholdOffDuration { get; set; }

    public string RainRateEntity { get; set; }
    public decimal? RainRateThreshold { get; set; }
    public TimeSpan? RainRateThresholdOnDuration { get; set; }
    public TimeSpan? RainRateThresholdOffDuration { get; set; }

    public string WeatherPredictionEntity { get; set; }
    public string IndoorTemperatureEntity { get; set; }
    public decimal? MaxIndoorTemperature { get; set; }
    public decimal? ConditionalIndoorTemperature { get; set; }
    public decimal? ConditionalOutdoorTemperaturePrediction { get; set; }
    public int ConditionalOutdoorTemperaturePredictionDays { get; set; }
    public TimeSpan MinimumIntervalSinceLastAutomatedAction { get; set; }
    public TimeSpan MinimumIntervalSinceLastManualAction { get; set; }
}

public enum ScreenAction
{
    None,
    Up,
    Down
}