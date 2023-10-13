// Use unique namespaces for your apps if you going to share with others to avoid
// conflicting names

using eLime.NetDaemonApps.Config;
using eLime.NetDaemonApps.Domain.Storage;
using NetDaemon.Extensions.MqttEntityManager;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;

namespace eLime.NetDaemonApps.apps.SmartIrrigation;

[NetDaemonApp(Id = "smartIrrigation")]
public class SmartIrrigation : IAsyncInitializable, IAsyncDisposable
{
    private readonly IHaContext _ha;
    private readonly IScheduler _scheduler;
    private readonly IMqttEntityManager _mqttEntityManager;
    private readonly IFileStorage _fileStorage;

    private readonly ILogger _logger;
    private readonly SmartIrrigationConfig _config;

    private Domain.SmartIrrigation.SmartIrrigation _smartIrrigation;

    private CancellationToken _ct;

    public SmartIrrigation(IHaContext ha, IScheduler scheduler, IAppConfig<SmartIrrigationConfig> config, IMqttEntityManager mqttEntityManager, IFileStorage fileStorage, ILogger<SmartIrrigation> logger)
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
            _smartIrrigation = _config.ToEntities(_ha, _scheduler, _mqttEntityManager, _fileStorage, _logger);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Something horrible happened :/");
        }

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _logger.LogInformation("Disposing Smart irrigation");

        _smartIrrigation.Dispose();


        _logger.LogInformation("Disposed Smart irrigation");


        return ValueTask.CompletedTask;
    }
}
