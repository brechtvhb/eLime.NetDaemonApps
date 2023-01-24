using eLime.NetDaemonApps.Config;
using eLime.NetDaemonApps.Domain.FlexiScreens;
using NetDaemon.Extensions.MqttEntityManager;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;

namespace eLime.NetDaemonApps.apps.SmartWasher;


[NetDaemonApp(Id = "smartwasher")]
public class SmartWasher : IAsyncInitializable, IAsyncDisposable
{
    private readonly IHaContext _ha;
    private readonly IScheduler _scheduler;
    private readonly IMqttEntityManager _mqttEntityManager;
    private readonly ILogger _logger;
    private readonly FlexiScreensConfig _config;
    private CancellationToken _ct;
    public List<FlexiScreen> Screen { get; set; } = new();
    public SmartWasher(IHaContext ha, IScheduler scheduler, IAppConfig<FlexiScreensConfig> config, IMqttEntityManager mqttEntityManager, ILogger<SmartWasher> logger)
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
