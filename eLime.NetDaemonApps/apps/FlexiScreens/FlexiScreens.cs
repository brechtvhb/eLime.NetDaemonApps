// Use unique namespaces for your apps if you going to share with others to avoid
// conflicting names

using eLime.NetDaemonApps.Config;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.Covers;
using eLime.NetDaemonApps.Domain.FlexiScreens;
using NetDaemon.Extensions.MqttEntityManager;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;

namespace eLime.NetDaemonApps.apps.FlexiScreens;

[Focus]
[NetDaemonApp(Id = "flexiscreens")]
public class FlexiScreens : IAsyncInitializable, IAsyncDisposable
{
    private readonly IHaContext _ha;
    private readonly IScheduler _scheduler;
    private readonly IMqttEntityManager _mqttEntityManager;
    private readonly ILogger<FlexiScreens> _logger;
    private readonly FlexiScreensConfig _config;
    private CancellationToken _ct;
    public List<FlexiScreen> Screen { get; set; } = new();
    public FlexiScreens(IHaContext ha, IScheduler scheduler, IAppConfig<FlexiScreensConfig> config, IMqttEntityManager mqttEntityManager, ILogger<FlexiScreens> logger)
    {
        _ha = ha;
        _scheduler = scheduler;
        _mqttEntityManager = mqttEntityManager;
        _logger = logger;
        _config = config.Value;
    }

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        _ct = cancellationToken;
        try
        {
            foreach (var (screenName, screenConfig) in _config.Screens)
            {
                var name = String.IsNullOrWhiteSpace(screenConfig.Name) ? screenName : screenConfig.Name;

                if (String.IsNullOrWhiteSpace(screenConfig.ScreenEntity))
                    throw new ArgumentNullException(nameof(screenConfig.ScreenEntity), "required");

                var screen = new Cover(_ha, screenConfig.ScreenEntity);

                var sunProtector = screenConfig.SunProtection.ToEntities(screenConfig.Orientation, _ha);
                var stormProtector = screenConfig.StormProtection.ToEntities(_ha);
                var temperatureProtector = screenConfig.TemperatureProtection.ToEntities(_ha);
                var manIsAngryProtector = screenConfig.MinimumIntervalSinceLastAutomatedAction != null ? new ManIsAngryProtector(screenConfig.MinimumIntervalSinceLastAutomatedAction) : null;
                var womanIsAngryProtector = screenConfig.MinimumIntervalSinceLastManualAction != null ? new WomanIsAngryProtector(screenConfig.MinimumIntervalSinceLastManualAction) : null;
                var childrenAreAngryProtector = screenConfig.SleepSensor != null ? new ChildrenAreAngryProtector(new BinarySensor(_ha, screenConfig.SleepSensor)) : null;
                var flexiScreen = new FlexiScreen(_ha, _logger, _scheduler, _mqttEntityManager, screenConfig.Enabled ?? true, name, screen, sunProtector, stormProtector, temperatureProtector, manIsAngryProtector, womanIsAngryProtector, childrenAreAngryProtector);
                Screen.Add(flexiScreen);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Something horrible happened :/");
        }

        return Task.CompletedTask;
    }


    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
