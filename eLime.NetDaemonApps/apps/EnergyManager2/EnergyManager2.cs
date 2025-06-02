using eLime.NetDaemonApps.Config;
using eLime.NetDaemonApps.Domain.EnergyManager2.Configuration;
using eLime.NetDaemonApps.Domain.Storage;
using NetDaemon.Extensions.MqttEntityManager;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable CS8618, CS9264

namespace eLime.NetDaemonApps.apps.EnergyManager2;

[NetDaemonApp(Id = "energyManager2"), Focus]
public class EnergyManager2 : IAsyncInitializable, IAsyncDisposable
{
    private readonly IHaContext _ha;
    private readonly IScheduler _scheduler;
    private readonly IMqttEntityManager _mqttEntityManager;
    private readonly IFileStorage _fileStorage;

    private readonly ILogger _logger;
    private readonly EnergyManagerConfig _config;

    private Domain.EnergyManager2.EnergyManager2 _energyManager;


    public EnergyManager2(IHaContext ha, IScheduler scheduler, IAppConfig<EnergyManagerConfig> config, IMqttEntityManager mqttEntityManager, IFileStorage fileStorage, ILogger<EnergyManager2> logger)
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
            var config = new EnergyManagerConfiguration(_ha, _logger, _scheduler, _fileStorage, _mqttEntityManager, _config);
            _energyManager = await Domain.EnergyManager2.EnergyManager2.Create(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Something horrible happened :/");
        }
    }

    public ValueTask DisposeAsync()
    {
        _logger.LogInformation("Disposing energy manager");
        _energyManager.Dispose();

        _logger.LogInformation("Disposed energy manager");

        return ValueTask.CompletedTask;
    }
}
