using eLime.NetDaemonApps.Config.FlexiScreens;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.Entities.Sun;
using eLime.NetDaemonApps.Domain.Entities.Weather;
using eLime.NetDaemonApps.Domain.FlexiScreens;

namespace eLime.NetDaemonApps.apps.FlexiScreens;

public static class ConfigExtensions
{

    public static SunProtector? ToEntities(this SunProtectionConfig sunProtectionConfig, int screenOrientation, IHaContext haContext)
    {
        if (String.IsNullOrWhiteSpace(sunProtectionConfig?.SunEntity))
            throw new ArgumentNullException(nameof(sunProtectionConfig.SunEntity), "required");


        var sun = new Sun(haContext, sunProtectionConfig.SunEntity);
        ScreenState? desiredStateBelowElevation = sunProtectionConfig.DesiredStateBelowElevationThreshold switch
        {
            ScreenAction.None => null,
            ScreenAction.Up => ScreenState.Up,
            ScreenAction.Down => ScreenState.Down
        };
        var sunProtector = new SunProtector(screenOrientation, sun, sunProtectionConfig.OrientationThreshold, sunProtectionConfig.ElevationThreshold, desiredStateBelowElevation);
        return sunProtector;
    }

    public static StormProtector? ToEntities(this StormProtectionConfig config, IHaContext ha)
    {
        if (config == null)
            return null;

        var windSpeedSensor = !String.IsNullOrWhiteSpace(config.WindSpeedEntity) ? new NumericSensor(ha, config.WindSpeedEntity) : null;
        var rainRateSensor = !String.IsNullOrWhiteSpace(config.RainRateEntity) ? new NumericSensor(ha, config.RainRateEntity) : null;
        var shortTermRainForecastSensor = !String.IsNullOrWhiteSpace(config.ShortTermRainForecastEntity) ? new NumericSensor(ha, config.ShortTermRainForecastEntity) : null;
        var stormProtector = new StormProtector(windSpeedSensor, config.WindSpeedStormStartThreshold, config.WindSpeedStormEndThreshold, rainRateSensor,
            config.RainRateStormStartThreshold, config.RainRateStormEndThreshold, shortTermRainForecastSensor,
            config.ShortTermRainStormStartThreshold, config.ShortTermRainStormEndThreshold);
        return stormProtector;
    }

    public static TemperatureProtector? ToEntities(this TemperatureProtectionConfig config, IHaContext ha)
    {
        if (config == null)
            return null;

        var solarLuxSensor = !String.IsNullOrWhiteSpace(config.SolarLuxSensor) ? new NumericSensor(ha, config.SolarLuxSensor) : null;
        var indoorTemperatureSensor = !String.IsNullOrWhiteSpace(config.IndoorTemperatureSensor) ? new NumericSensor(ha, config.IndoorTemperatureSensor) : null;
        var weather = !String.IsNullOrWhiteSpace(config.WeatherEntity) ? new Weather(ha, config.WeatherEntity) : null;

        var temperatureProtector = new TemperatureProtector(solarLuxSensor, config.SolarLuxAboveThreshold, config.SolarLuxBelowThreshold, indoorTemperatureSensor, config.MaxIndoorTemperature,
            config.ConditionalMaxIndoorTemperature, weather, config.ConditionalOutdoorTemperaturePrediction, config.ConditionalOutdoorTemperaturePredictionDays);

        return temperatureProtector;
    }
}