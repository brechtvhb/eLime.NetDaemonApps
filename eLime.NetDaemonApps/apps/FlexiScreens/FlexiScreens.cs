// Use unique namespaces for your apps if you going to share with others to avoid
// conflicting names

using eLime.netDaemonApps.Config;
using eLime.NetDaemonApps.Domain.Rooms;
using NetDaemon.Extensions.MqttEntityManager;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;
using eLime.NetDaemonApps.Config;
using eLime.NetDaemonApps.Domain.FlexiScreens;

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
            foreach (var (roomName, screenConfig) in _config.Screens)
            {
                if (String.IsNullOrWhiteSpace(screenConfig.Name))
                    screenConfig.Name = roomName;

                var screen = new FlexiScreen(_ha, _logger, _scheduler, _mqttEntityManager, screenConfig);
                Screen.Add(screen);
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
