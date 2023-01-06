namespace eLime.NetDaemonApps.Config.FlexiScreens;

public class TemperatureProtectionConfig
{
    public string? SolarLuxSensor { get; set; }
    public int? SolarLuxAboveThreshold { get; set; }
    public int? SolarLuxBelowThreshold { get; set; }
    public string? IndoorTemperatureSensor { get; set; }
    public double? MaxIndoorTemperature { get; set; }
    public string? WeatherEntity { get; set; }
    public double? ConditionalMaxIndoorTemperature { get; set; }
    public double? ConditionalOutdoorTemperaturePrediction { get; set; }
    public int? ConditionalOutdoorTemperaturePredictionDays { get; set; }
}