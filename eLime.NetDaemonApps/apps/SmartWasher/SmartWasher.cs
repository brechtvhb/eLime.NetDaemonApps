using eLime.NetDaemonApps.Config.SmartWasher;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using NetDaemon.Extensions.MqttEntityManager;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;

namespace eLime.NetDaemonApps.apps.SmartWasher;

/// <summary>
/// Peak usage per state
/// Prewashing (0-10): 120 W
/// Heating (10-25): 2200 W
/// Washing (0-10): 170 W
/// Rinsing (45 min): 330 W
/// Spinning (10): 420 W
/// </summary>
[Focus]
[NetDaemonApp(Id = "smartwasher")]
public class SmartWasher : IAsyncInitializable, IAsyncDisposable
{
    private readonly IHaContext _ha;
    private readonly IScheduler _scheduler;
    private readonly IMqttEntityManager _mqttEntityManager;
    private readonly ILogger _logger;
    private readonly SmartWasherConfig _config;
    private CancellationToken _ct;
    public Domain.SmartWashers.SmartWasher Washer { get; set; }
    public SmartWasher(IHaContext ha, IScheduler scheduler, IAppConfig<SmartWasherConfig> config, IMqttEntityManager mqttEntityManager, ILogger<SmartWasher> logger)
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
            var powerSocket = BinarySwitch.Create(_ha, _config.PowerSocket);
            var powerSensor = NumericSensor.Create(_ha, _config.PowerSensor);

            Washer = new Domain.SmartWashers.SmartWasher(_logger, _ha, _mqttEntityManager, _scheduler, _config.Enabled ?? true, _config.Name, powerSocket, powerSensor);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Something horrible happened :/");
        }

        return Task.CompletedTask;
    }



    public ValueTask DisposeAsync()
    {
        _logger.LogInformation("Disposing smart washer");

        Washer.Dispose();

        return ValueTask.CompletedTask;
    }
}
