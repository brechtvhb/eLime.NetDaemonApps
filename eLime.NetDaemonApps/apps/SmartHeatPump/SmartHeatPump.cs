using eLime.NetDaemonApps.Config;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.SmartHeatPump;
using eLime.NetDaemonApps.Domain.Storage;
using NetDaemon.Extensions.MqttEntityManager;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;

namespace eLime.NetDaemonApps.apps.SmartHeatPump;


[NetDaemonApp(Id = "smartHeatPump"), Focus]
public class SmartHeatPump : IAsyncInitializable, IAsyncDisposable
{
    private readonly IHaContext _ha;
    private readonly IScheduler _scheduler;
    private readonly IMqttEntityManager _mqttEntityManager;
    private readonly IFileStorage _fileStorage;

    private readonly ILogger _logger;
    private readonly SmartHeatPumpConfig _config;

    private Domain.SmartHeatPump.SmartHeatPump _smartHeatPump;

    private CancellationToken _ct;

    public SmartHeatPump(IHaContext ha, IScheduler scheduler, IAppConfig<SmartHeatPumpConfig> config, IMqttEntityManager mqttEntityManager, IFileStorage fileStorage, ILogger<SmartHeatPump> logger)
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
            var smartGridReadyInput1 = new BinarySwitch(_ha, _config.SmartGridReadyInput1);
            var smartGridReadyInput2 = new BinarySwitch(_ha, _config.SmartGridReadyInput2);

            var config = new SmartHeatPumpConfiguration
            {
                HaContext = _ha,
                Scheduler = _scheduler,
                MqttEntityManager = _mqttEntityManager,
                FileStorage = _fileStorage,
                Logger = _logger,
                DebounceDuration = TimeSpan.FromSeconds(1),
                SmartGridReadyInput1 = smartGridReadyInput1,
                SmartGridReadyInput2 = smartGridReadyInput2
            };

            _smartHeatPump = new Domain.SmartHeatPump.SmartHeatPump(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Something horrible happened :/");
        }

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _logger.LogInformation("Disposing smart heat pump");
        _smartHeatPump.Dispose();

        _logger.LogInformation("Disposed smart heat pump");

        return ValueTask.CompletedTask;
    }
}
