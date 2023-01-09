// Use unique namespaces for your apps if you going to share with others to avoid
// conflicting names

using eLime.NetDaemonApps.Config;
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

                var flexiScreen = screenConfig.ToEntities(_ha, _scheduler, _mqttEntityManager, _logger, _config.NetDaemonUserId, name);
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
