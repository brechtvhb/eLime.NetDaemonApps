// Use unique namespaces for your apps if you going to share with others to avoid
// conflicting names

using eLime.NetDaemonApps.Config;
using eLime.NetDaemonApps.Domain.Storage;
using NetDaemon.Extensions.MqttEntityManager;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;

namespace eLime.NetDaemonApps.apps.SmartVentilation;

[Focus]
[NetDaemonApp(Id = "smartVentilation")]
public class SmartVentilation : IAsyncInitializable, IAsyncDisposable
{
    private readonly IHaContext _ha;
    private readonly IScheduler _scheduler;
    private readonly IMqttEntityManager _mqttEntityManager;
    private readonly IFileStorage _fileStorage;

    private readonly ILogger _logger;
    private readonly SmartVentilationConfig _config;

    private Domain.SmartVentilation.SmartVentilation _smartVentilation;

    private CancellationToken _ct;

    public SmartVentilation(IHaContext ha, IScheduler scheduler, IAppConfig<SmartVentilationConfig> config, IMqttEntityManager mqttEntityManager, IFileStorage fileStorage, ILogger<SmartVentilation> logger)
    {
        _ha = ha;
        _scheduler = scheduler;
        _mqttEntityManager = mqttEntityManager;
        _fileStorage = fileStorage;
        _logger = logger;
        _config = config.Value;
    }

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        _ct = cancellationToken;
        try
        {
            _smartVentilation = _config.ToEntities(_ha, _scheduler, _mqttEntityManager, _fileStorage, _logger, _config.NetDaemonUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Something horrible happened :/");
        }

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _logger.LogInformation("Disposing Smart ventilation");

        _smartVentilation.Dispose();


        _logger.LogInformation("Disposed Smart ventilation");


        return ValueTask.CompletedTask;
    }
}
