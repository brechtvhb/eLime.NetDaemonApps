// Use unique namespaces for your apps if you going to share with others to avoid
// conflicting names

using eLime.NetDaemonApps.Config;
using eLime.NetDaemonApps.Domain.Storage;
using NetDaemon.Extensions.MqttEntityManager;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;

namespace eLime.NetDaemonApps.apps.CapacityCalculator;

[Focus]
[NetDaemonApp(Id = "capacityCalculator")]
public class CapacityCalculator : IAsyncInitializable, IAsyncDisposable
{
    private readonly IHaContext _ha;
    private readonly IScheduler _scheduler;
    private readonly IMqttEntityManager _mqttEntityManager;
    private readonly IFileStorage _fileStorage;

    private readonly ILogger _logger;
    private readonly CapacityCalculatorConfig _config;

    private Domain.CapacityCalculator.CapacityCalculator _capacityCalculator;

    private CancellationToken _ct;

    public CapacityCalculator(IHaContext ha, IScheduler scheduler, IAppConfig<CapacityCalculatorConfig> config, IMqttEntityManager mqttEntityManager, IFileStorage fileStorage, ILogger<CapacityCalculator> logger)
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
            _capacityCalculator = new Domain.CapacityCalculator.CapacityCalculator(_ha, _logger, _scheduler, _mqttEntityManager, _fileStorage, _config.SmartMeterUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Something horrible happened :/");
        }

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _logger.LogInformation("Disposing Capacity calculator");

        _capacityCalculator.Dispose();


        _logger.LogInformation("Disposed Capacity calculator");


        return ValueTask.CompletedTask;
    }
}
