using eLime.NetDaemonApps.Config.FlexiScreens;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.Covers;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.Entities.Sun;
using eLime.NetDaemonApps.Domain.Entities.Weather;
using eLime.NetDaemonApps.Domain.FlexiScreens;
using NetDaemon.Extensions.MqttEntityManager;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.apps.FlexiScreens;

public static class ConfigExtensions
{

    public static FlexiScreen ToEntities(this FlexiScreenConfig config, IHaContext ha, IScheduler scheduler, IMqttEntityManager mqttEntityManager, ILogger logger, String netDaemonUserId, string name)
    {
        var screen = new Cover(ha, config.ScreenEntity);
        var sun = new Sun(ha, config.SunProtection.SunEntity);
        var windSpeedSensor = !String.IsNullOrWhiteSpace(config.StormProtection?.WindSpeedEntity) ? NumericThresholdSensor.Create(ha, config.StormProtection.WindSpeedEntity, config.StormProtection.WindSpeedStormStartThreshold, config.StormProtection.WindSpeedStormEndThreshold) : null;
        var rainRateSensor = !String.IsNullOrWhiteSpace(config.StormProtection?.RainRateEntity) ? NumericThresholdSensor.Create(ha, config.StormProtection.RainRateEntity, config.StormProtection.RainRateStormStartThreshold, config.StormProtection.RainRateStormEndThreshold) : null;
        var shortTermRainForecastSensor = !String.IsNullOrWhiteSpace(config.StormProtection?.ShortTermRainForecastEntity) ? NumericThresholdSensor.Create(ha, config.StormProtection.ShortTermRainForecastEntity, config.StormProtection.ShortTermRainStormStartThreshold, config.StormProtection.ShortTermRainStormEndThreshold) : null;
        var solarLuxSensor = !String.IsNullOrWhiteSpace(config.TemperatureProtection?.SolarLuxSensor) ? NumericThresholdSensor.Create(ha, config.TemperatureProtection.SolarLuxSensor, config.TemperatureProtection.SolarLuxAboveThreshold, config.TemperatureProtection.SolarLuxBelowThreshold) : null;
        var indoorTemperatureSensor = !String.IsNullOrWhiteSpace(config.TemperatureProtection?.IndoorTemperatureSensor) ? NumericSensor.Create(ha, config.TemperatureProtection.IndoorTemperatureSensor) : null;
        var weather = !String.IsNullOrWhiteSpace(config.TemperatureProtection?.WeatherEntity) ? new Weather(ha, config.TemperatureProtection.WeatherEntity) : null;
        var hourlyWeather = !String.IsNullOrWhiteSpace(config.StormProtection?.HourlyWeatherEntity) ? new Weather(ha, config.StormProtection.HourlyWeatherEntity) : null;

        var sunProtector = config.SunProtection.ToEntities(sun, config.Orientation);
        var stormProtector = config.StormProtection.ToEntities(windSpeedSensor, rainRateSensor, shortTermRainForecastSensor, hourlyWeather);
        var temperatureProtector = config.TemperatureProtection.ToEntities(solarLuxSensor, indoorTemperatureSensor, weather);

        var manIsAngryProtector = config.MinimumIntervalSinceLastAutomatedAction != null
            ? new ManIsAngryProtector(config.MinimumIntervalSinceLastAutomatedAction)
            : new ManIsAngryProtector(TimeSpan.FromMinutes(15));
        var womanIsAngryProtector = config.MinimumIntervalSinceLastManualAction != null
            ? new WomanIsAngryProtector(config.MinimumIntervalSinceLastManualAction)
            : new WomanIsAngryProtector(TimeSpan.FromHours(1));

        var childrenAreAngryProtector = config.SleepSensor != null ? new ChildrenAreAngryProtector(BinarySensor.Create(ha, config.SleepSensor)) : null;

        var flexiScreen = new FlexiScreen(ha, logger, scheduler, mqttEntityManager, config.Enabled ?? true, name, screen, netDaemonUserId, sunProtector, stormProtector,
            temperatureProtector, manIsAngryProtector, womanIsAngryProtector, childrenAreAngryProtector, TimeSpan.FromSeconds(3));
        return flexiScreen;
    }

    public static SunProtector ToEntities(this SunProtectionConfig sunProtectionConfig, Sun sun, int screenOrientation)
    {
        if (sun == null)
            throw new ArgumentNullException(nameof(sunProtectionConfig.SunEntity), "required");


        ScreenState? desiredStateBelowElevation = sunProtectionConfig.DesiredStateBelowElevationThreshold switch
        {
            ScreenAction.None => null,
            ScreenAction.Up => ScreenState.Up,
            ScreenAction.Down => ScreenState.Down
        };
        var sunProtector = new SunProtector(screenOrientation, sun, sunProtectionConfig.OrientationThreshold, sunProtectionConfig.ElevationThreshold, desiredStateBelowElevation);
        return sunProtector;
    }

    public static StormProtector? ToEntities(this StormProtectionConfig? config, NumericThresholdSensor? windSpeedSensor, NumericThresholdSensor? rainRateSensor, NumericThresholdSensor? shortTermRainForecastSensor, Weather? hourlyWeather)
    {
        if (config == null)
            return null;

        var stormProtector = new StormProtector(windSpeedSensor, config.WindSpeedStormStartThreshold, config.WindSpeedStormEndThreshold, rainRateSensor,
        config.RainRateStormStartThreshold, config.RainRateStormEndThreshold, shortTermRainForecastSensor,
        config.ShortTermRainStormStartThreshold, config.ShortTermRainStormEndThreshold, hourlyWeather, config.NightlyPredictionHours, config.NightlyWindSpeedThreshold, config.NightlyRainThreshold, config.NightlyRainRateThreshold);
        return stormProtector;
    }

    public static TemperatureProtector? ToEntities(this TemperatureProtectionConfig? config, NumericThresholdSensor? solarLuxSensor, NumericSensor? indoorTemperatureSensor, Weather? weather)
    {
        if (config == null)
            return null;

        var temperatureProtector = new TemperatureProtector(solarLuxSensor, config.SolarLuxAboveThreshold, config.SolarLuxBelowThreshold, indoorTemperatureSensor, config.MaxIndoorTemperature,
            config.ConditionalMaxIndoorTemperature, weather, config.ConditionalOutdoorTemperaturePrediction, config.ConditionalOutdoorTemperaturePredictionDays);

        return temperatureProtector;
    }
}