using eLime.NetDaemonApps.apps.FlexiScreens;
using eLime.NetDaemonApps.Config.FlexiScreens;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.Covers;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.Entities.Sun;
using eLime.NetDaemonApps.Domain.Entities.Weather;
using eLime.NetDaemonApps.Domain.FlexiScreens;
using eLime.NetDaemonApps.Tests.Helpers;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;

namespace eLime.NetDaemonApps.Tests.Builders;

public class ScreenBuilder
{
    private readonly AppTestContext _testCtx;
    private readonly ILogger _logger;
    private readonly IMqttEntityManager _mqttEntityManager;

    private FlexiScreenConfig _config;
    private Cover? _cover;
    private Sun? _sun;

    private NumericThresholdSensor? _windSpeedSensor;
    private NumericThresholdSensor? _rainRateSensor;
    private NumericThresholdSensor? _shortTermRainForecastSensor;

    private NumericThresholdSensor? _solarLuxSensor;
    private NumericSensor? _indoorTemperatureSensor;
    private Weather? _weather;
    private Weather? _hourluWeather;
    private BinarySensor? _sleepSensor;

    public ScreenBuilder(AppTestContext testCtx, ILogger logger, IMqttEntityManager mqttEntityManager)
    {
        _testCtx = testCtx;
        _logger = logger;
        _mqttEntityManager = mqttEntityManager;
        _config = new FlexiScreenConfig
        {
            Name = "Office",
            Enabled = true,
            ScreenEntity = "cover.office",
            Orientation = 265,
            SunProtection = new SunProtectionConfig
            {
                SunEntity = "sun.sun",
                ElevationThreshold = 10,
                OrientationThreshold = 85,
                DesiredStateBelowElevationThreshold = ScreenAction.Down
            }
        };
    }

    public ScreenBuilder WithCover(Cover cover)
    {
        _cover = cover;
        return this;
    }

    public ScreenBuilder WithSun(Sun sun)
    {
        _sun = sun;
        return this;
    }
    public ScreenBuilder WithWindSpeedSensor(NumericThresholdSensor windSpeedSensor)
    {
        _windSpeedSensor = windSpeedSensor;
        _config.StormProtection ??= new StormProtectionConfig();
        _config.StormProtection.WindSpeedStormStartThreshold = windSpeedSensor.Threshold;
        _config.StormProtection.WindSpeedStormEndThreshold = windSpeedSensor.BelowThreshold;

        return this;
    }
    public ScreenBuilder WithRainRateSensor(NumericThresholdSensor rainRateSensor)
    {
        _rainRateSensor = rainRateSensor;
        _config.StormProtection ??= new StormProtectionConfig();
        _config.StormProtection.RainRateStormStartThreshold = rainRateSensor.Threshold;
        _config.StormProtection.RainRateStormEndThreshold = rainRateSensor.BelowThreshold;
        return this;
    }
    public ScreenBuilder WithShortTermRainRateSensor(NumericThresholdSensor shortTermRainForecastSensor)
    {
        _shortTermRainForecastSensor = shortTermRainForecastSensor;
        _config.StormProtection ??= new StormProtectionConfig();
        _config.StormProtection.ShortTermRainStormStartThreshold = shortTermRainForecastSensor.Threshold;
        _config.StormProtection.ShortTermRainStormEndThreshold = shortTermRainForecastSensor.BelowThreshold;
        return this;
    }

    public ScreenBuilder WithSolarLuxSensor(NumericThresholdSensor solarLuxSensor)
    {
        _solarLuxSensor = solarLuxSensor;
        _config.TemperatureProtection ??= new TemperatureProtectionConfig();
        _config.TemperatureProtection.SolarLuxAboveThreshold = solarLuxSensor.Threshold;
        _config.TemperatureProtection.SolarLuxBelowThreshold = solarLuxSensor.BelowThreshold;
        return this;
    }

    public ScreenBuilder WithIndoorTemperatureSensor(NumericSensor indoorTemperatureSensor, double? maxIndoorTemperature)
    {
        _indoorTemperatureSensor = indoorTemperatureSensor;
        _config.TemperatureProtection ??= new TemperatureProtectionConfig();
        _config.TemperatureProtection.MaxIndoorTemperature = maxIndoorTemperature;
        return this;
    }

    public ScreenBuilder WithWeatherForecast(Weather weather, double? conditionalMaxIndoorTemperature, double? conditionalMaxOutdoorTemperature, int? conditionalPredictionDays)
    {
        _weather = weather;
        _config.TemperatureProtection ??= new TemperatureProtectionConfig();
        _config.TemperatureProtection.ConditionalMaxIndoorTemperature = conditionalMaxIndoorTemperature;
        _config.TemperatureProtection.ConditionalOutdoorTemperaturePrediction = conditionalMaxOutdoorTemperature;
        _config.TemperatureProtection.ConditionalOutdoorTemperaturePredictionDays = conditionalPredictionDays;
        return this;
    }
    public ScreenBuilder WithHourlyWeatherForecast(Weather weather, int? nightlyPredictionHours, double? nightlyWindSpeedThreshold, double? nightlyRainThreshold)
    {
        _hourluWeather = weather;

        _config.StormProtection ??= new StormProtectionConfig();
        _config.StormProtection.NightlyPredictionHours = nightlyPredictionHours;
        _config.StormProtection.NightlyWindSpeedThreshold = nightlyWindSpeedThreshold;
        _config.StormProtection.NightlyRainThreshold = nightlyRainThreshold;
        return this;
    }

    public ScreenBuilder WithSleepSensor(BinarySensor sleepSensor)
    {
        _sleepSensor = sleepSensor;
        return this;
    }

    public ScreenBuilder WithMinimumIntervalSinceLastAutomatedAction(TimeSpan span)
    {
        _config.MinimumIntervalSinceLastAutomatedAction = span;
        return this;
    }
    public ScreenBuilder WithMinimumIntervalSinceLastManualAction(TimeSpan span)
    {
        _config.MinimumIntervalSinceLastManualAction = span;
        return this;
    }

    public ScreenBuilder WithDesiredActionOnBelowElevation(ScreenAction action)
    {
        _config.SunProtection.DesiredStateBelowElevationThreshold = action;
        return this;
    }

    public FlexiScreen Build()
    {
        var screen = _cover ?? new Cover(_testCtx.HaContext, _config.ScreenEntity);
        var sun = _sun ?? new Sun(_testCtx.HaContext, _config.SunProtection.SunEntity);

        var windSpeedSensor = _windSpeedSensor ?? (_config.StormProtection?.WindSpeedEntity != null ? NumericThresholdSensor.Create(_testCtx.HaContext, _config.StormProtection.WindSpeedEntity, _config.StormProtection.WindSpeedStormStartThreshold, _config.StormProtection.WindSpeedStormEndThreshold) : null);
        var rainRateSensor = _rainRateSensor ?? (_config.StormProtection?.RainRateEntity != null ? NumericThresholdSensor.Create(_testCtx.HaContext, _config.StormProtection.RainRateEntity, _config.StormProtection.RainRateStormStartThreshold, _config.StormProtection.RainRateStormEndThreshold) : null);
        var forecastRainSensor = _shortTermRainForecastSensor ?? (_config.StormProtection?.ShortTermRainForecastEntity != null ? NumericThresholdSensor.Create(_testCtx.HaContext, _config.StormProtection.ShortTermRainForecastEntity, _config.StormProtection.ShortTermRainStormStartThreshold, _config.StormProtection.ShortTermRainStormStartThreshold) : null);

        var solarLuxSensor = _solarLuxSensor ?? (_config.TemperatureProtection?.SolarLuxSensor != null ? NumericThresholdSensor.Create(_testCtx.HaContext, _config.TemperatureProtection.SolarLuxSensor, _config.TemperatureProtection.SolarLuxAboveThreshold, _config.TemperatureProtection.SolarLuxBelowThreshold) : null);
        var indoorTemperatureSensor = _indoorTemperatureSensor ?? (_config.TemperatureProtection?.IndoorTemperatureSensor != null ? NumericSensor.Create(_testCtx.HaContext, _config.TemperatureProtection.IndoorTemperatureSensor) : null);
        var weather = _weather ?? (_config.TemperatureProtection?.WeatherEntity != null ? new Weather(_testCtx.HaContext, _config.TemperatureProtection.WeatherEntity) : null);
        var hourlyWeather = _hourluWeather ?? (_config.StormProtection?.HourlyWeatherEntity != null ? new Weather(_testCtx.HaContext, _config.StormProtection.HourlyWeatherEntity) : null);

        var sleepSensor = _sleepSensor ?? (_config.SleepSensor != null ? BinarySensor.Create(_testCtx.HaContext, _config.SleepSensor) : null);

        var sunProtector = _config.SunProtection.ToEntities(sun, _config.Orientation);
        var stormProtector = _config.StormProtection.ToEntities(windSpeedSensor, rainRateSensor, forecastRainSensor, hourlyWeather);
        var temperatureProtector = _config.TemperatureProtection.ToEntities(solarLuxSensor, indoorTemperatureSensor, weather);
        var manIsAngryProtector = _config.MinimumIntervalSinceLastAutomatedAction != null ? new ManIsAngryProtector(_config.MinimumIntervalSinceLastAutomatedAction) : new ManIsAngryProtector(TimeSpan.FromMinutes(15));
        var womanIsAngryProtector = _config.MinimumIntervalSinceLastManualAction != null ? new WomanIsAngryProtector(_config.MinimumIntervalSinceLastManualAction) : new WomanIsAngryProtector(TimeSpan.FromHours(1));

        var childrenAreAngryProtector = sleepSensor != null ? new ChildrenAreAngryProtector(sleepSensor) : null;

        var flexiScreen = new FlexiScreen(_testCtx.HaContext, _logger, _testCtx.Scheduler, _mqttEntityManager, _config.Enabled ?? true, _config.Name, screen, "somecoolid", sunProtector, stormProtector, temperatureProtector, manIsAngryProtector, womanIsAngryProtector, childrenAreAngryProtector, TimeSpan.Zero);
        return flexiScreen;
    }
}