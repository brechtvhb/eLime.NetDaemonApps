using eLime.NetDaemonApps.Config;
using eLime.NetDaemonApps.Domain.EnergyManager;
using eLime.NetDaemonApps.Domain.Storage;
using NetDaemon.Extensions.MqttEntityManager;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable CS8618, CS9264

namespace eLime.NetDaemonApps.apps.EnergyManager;

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


    public EnergyManager(IHaContext ha, IScheduler scheduler, IAppConfig<EnergyManagerConfig> config, IMqttEntityManager mqttEntityManager, IFileStorage fileStorage, ILogger<EnergyManager> logger)
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
            var config = new EnergyManagerConfiguration(_ha, _logger, _scheduler, _fileStorage, _mqttEntityManager, _config, TimeSpan.FromSeconds(1));
            _energyManager = await Domain.EnergyManager.EnergyManager.Create(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Something horrible happened :/");
        }
    }

    public ValueTask DisposeAsync()
    {
        _energyManager.Dispose();
        _logger.LogInformation("Disposed energy manager");
        return ValueTask.CompletedTask;
    }
}
