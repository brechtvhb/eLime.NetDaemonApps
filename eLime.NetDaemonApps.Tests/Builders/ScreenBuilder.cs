using eLime.NetDaemonApps.apps.FlexiScreens;
using eLime.NetDaemonApps.Config.FlexiScreens;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.Covers;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.Entities.Sun;
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

    public FlexiScreen Build()
    {
        var screen = _cover ?? new Cover(_testCtx.HaContext, _config.ScreenEntity);
        var sun = _sun ?? new Sun(_testCtx.HaContext, _config.SunProtection.SunEntity);
        var sunProtector = _config.SunProtection.ToEntities(sun, _config.Orientation);
        var stormProtector = _config.StormProtection.ToEntities(_windSpeedSensor, _rainRateSensor, _shortTermRainForecastSensor);
        var temperatureProtector = _config.TemperatureProtection.ToEntities(_testCtx.HaContext);
        var manIsAngryProtector = _config.MinimumIntervalSinceLastAutomatedAction != null ? new ManIsAngryProtector(_config.MinimumIntervalSinceLastAutomatedAction) : new ManIsAngryProtector(TimeSpan.FromMinutes(15));
        var womanIsAngryProtector = _config.MinimumIntervalSinceLastManualAction != null ? new WomanIsAngryProtector(_config.MinimumIntervalSinceLastManualAction) : new WomanIsAngryProtector(TimeSpan.FromHours(1));
        var childrenAreAngryProtector = _config.SleepSensor != null ? new ChildrenAreAngryProtector(new BinarySensor(_testCtx.HaContext, _config.SleepSensor)) : null;
        var flexiScreen = new FlexiScreen(_testCtx.HaContext, _logger, _testCtx.Scheduler, _mqttEntityManager, _config.Enabled ?? true, _config.Name, screen, "somecoolid", sunProtector, stormProtector, temperatureProtector, manIsAngryProtector, womanIsAngryProtector, childrenAreAngryProtector);
        return flexiScreen;
    }
}