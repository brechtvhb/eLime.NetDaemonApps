using eLime.NetDaemonApps.Config;
using eLime.NetDaemonApps.Domain.SmartHeatPump;
using eLime.NetDaemonApps.Domain.Storage;
using NetDaemon.Extensions.MqttEntityManager;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;
#pragma warning disable CS8618, CS9264

namespace eLime.NetDaemonApps.apps.SmartHeatPump;

[NetDaemonApp(Id = "smartHeatPump")]
public class SmartHeatPump : IAsyncInitializable, IAsyncDisposable
{
    private readonly IHaContext _ha;
    private readonly IScheduler _scheduler;
    private readonly IMqttEntityManager _mqttEntityManager;
    private readonly IFileStorage _fileStorage;

    private readonly ILogger _logger;
    private readonly SmartHeatPumpConfig _config;

    private Domain.SmartHeatPump.SmartHeatPump _smartHeatPump;


    public SmartHeatPump(IHaContext ha, IScheduler scheduler, IAppConfig<SmartHeatPumpConfig> config, IMqttEntityManager mqttEntityManager, IFileStorage fileStorage, ILogger<SmartHeatPump> logger)
    {
        _ha = ha;
        _scheduler = scheduler;
        _mqttEntityManager = mqttEntityManager;
        _fileStorage = fileStorage;
        _logger = logger;
        _config = config.Value;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            var config = new SmartHeatPumpConfiguration(_ha, _logger, _scheduler, _fileStorage, _mqttEntityManager, _config, TimeSpan.FromSeconds(1));
            _smartHeatPump = await Domain.SmartHeatPump.SmartHeatPump.Create(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Something horrible happened :/");
        }
    }

    public ValueTask DisposeAsync()
    {
        _smartHeatPump.Dispose();
        _logger.LogInformation("Disposed smart heat pump");
        return ValueTask.CompletedTask;
    }
}
