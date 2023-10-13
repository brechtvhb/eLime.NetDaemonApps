using eLime.NetDaemonApps.Config;
using eLime.NetDaemonApps.Domain.Storage;
using NetDaemon.Extensions.MqttEntityManager;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;

namespace eLime.NetDaemonApps.apps.EnergyManager;

[Focus]
[NetDaemonApp(Id = "energyManager")]
public class EnergyManager : IAsyncInitializable, IAsyncDisposable
{
    private readonly IHaContext _ha;
    private readonly IScheduler _scheduler;
    private readonly IMqttEntityManager _mqttEntityManager;
    private readonly IFileStorage _fileStorage;
    private readonly ILogger _logger;
    private readonly EnergyManagerConfig _config;

    private Domain.EnergyManager.EnergyManager _energyManager;

    private CancellationToken _ct;

    public EnergyManager(IHaContext ha, IScheduler scheduler, IAppConfig<EnergyManagerConfig> config, IMqttEntityManager mqttEntityManager, IFileStorage fileStorage, ILogger<EnergyManager> logger)
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
            _energyManager = _config.ToEntities(_ha, _scheduler, _mqttEntityManager, _fileStorage, _logger);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Something horrible happened :/");
        }

        return Task.CompletedTask;
    }



    public ValueTask DisposeAsync()
    {
        _logger.LogInformation("Disposing Energy manager");

        _energyManager.Dispose();

        _logger.LogInformation("Disposed Energy manager");

        return ValueTask.CompletedTask;
    }
}
