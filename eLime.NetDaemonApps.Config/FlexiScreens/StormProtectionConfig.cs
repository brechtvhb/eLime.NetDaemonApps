namespace eLime.NetDaemonApps.Config.FlexiScreens;

public class StormProtectionConfig
{
    public string? WindSpeedEntity { get; set; }
    public double? WindSpeedStormStartThreshold { get; set; }
    public double? WindSpeedStormEndThreshold { get; set; }
    public string? RainRateEntity { get; set; }
    public double? RainRateStormStartThreshold { get; set; }
    public double? RainRateStormEndThreshold { get; set; }
    public string? ShortTermRainForecastEntity { get; set; }
    public double? ShortTermRainStormStartThreshold { get; set; }
    public double? ShortTermRainStormEndThreshold { get; set; }

    public string? HourlyWeatherEntity { get; set; }
    public int? NightlyPredictionHours { get; set; }
    public double? NightlyWindSpeedThreshold { get; set; }
    public double? NightlyRainThreshold { get; set; }
    public double? NightlyRainRateThreshold { get; set; }
}