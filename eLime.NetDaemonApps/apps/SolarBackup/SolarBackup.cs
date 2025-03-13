using eLime.NetDaemonApps.Config;
using eLime.NetDaemonApps.Domain.Entities.Buttons;
using eLime.NetDaemonApps.Domain.SolarBackup.Clients;
using eLime.NetDaemonApps.Domain.Storage;
using NetDaemon.Extensions.MqttEntityManager;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;

namespace eLime.NetDaemonApps.apps.SolarBackup;

[NetDaemonApp(Id = "solarBackup")]
public class SolarBackup : IAsyncInitializable, IAsyncDisposable
{
    private readonly IHaContext _ha;
    private readonly IScheduler _scheduler;
    private readonly IMqttEntityManager _mqttEntityManager;
    private readonly IFileStorage _fileStorage;

    private readonly ILogger _logger;
    private readonly SolarBackupConfig _config;

    private Domain.SolarBackup.SolarBackup _solarBackup;

    private CancellationToken _ct;

    public SolarBackup(IHaContext ha, IScheduler scheduler, IAppConfig<SolarBackupConfig> config, IMqttEntityManager mqttEntityManager, IFileStorage fileStorage, ILogger<SolarBackup> logger)
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
            var pveClient = new PveClient(_logger, _config.Pve.Url, _config.Pve.Token, _config.Pve.Cluster, _config.Pve.StorageId, _config.Pve.StorageName);
            var pbsClient = new PbsClient(_logger, _config.Pbs.Url, _config.Pbs.Token, _config.Pbs.DataStore, _config.Pbs.VerifyJobId, _config.Pbs.PruneJobId);
            var shutDownButton = new Button(_ha, _config.Synology.ShutDownButton);

            _solarBackup = new Domain.SolarBackup.SolarBackup(_logger, _ha, _scheduler, _fileStorage, _mqttEntityManager, _config.Synology.Mac, _config.Synology.BroadcastAddress, pveClient, pbsClient, shutDownButton, _config.BackupInterval, _config.CriticalBackupInterval);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Something horrible happened :/");
        }

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _logger.LogInformation("Disposing solar backup");
        _solarBackup?.Dispose();

        _logger.LogInformation("Disposed solar backup");

        return ValueTask.CompletedTask;
    }
}
