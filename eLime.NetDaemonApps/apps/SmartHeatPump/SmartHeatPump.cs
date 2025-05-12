using eLime.NetDaemonApps.Config;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.Entities.TextSensors;
using eLime.NetDaemonApps.Domain.SmartHeatPump;
using eLime.NetDaemonApps.Domain.Storage;
using NetDaemon.Extensions.MqttEntityManager;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;
#pragma warning disable CS8618, CS9264

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
            var config = new SmartHeatPumpConfiguration
            {
                HaContext = _ha,
                Scheduler = _scheduler,
                MqttEntityManager = _mqttEntityManager,
                FileStorage = _fileStorage,
                Logger = _logger,
                DebounceDuration = TimeSpan.FromSeconds(1),
                SmartGridReadyInput1 = BinarySwitch.Create(_ha, _config.SmartGridReadyInput1),
                SmartGridReadyInput2 = BinarySwitch.Create(_ha, _config.SmartGridReadyInput2),
                SourcePumpRunningSensor = BinarySensor.Create(_ha, _config.SourcePumpRunningSensor),
                SourceTemperatureSensor = NumericSensor.Create(_ha, _config.SourceTemperatureSensor),
                IsCoolingSensor = BinarySensor.Create(_ha, _config.IsCoolingSensor),
                StatusBytesSensor = TextSensor.Create(_ha, _config.StatusBytesSensor),
                HeatConsumedTodayIntegerSensor = NumericSensor.Create(_ha, _config.HeatConsumedTodayIntegerSensor),
                HeatConsumedTodayDecimalsSensor = NumericSensor.Create(_ha, _config.HeatConsumedTodayDecimalsSensor),
                HeatProducedTodayIntegerSensor = NumericSensor.Create(_ha, _config.HeatProducedTodayIntegerSensor),
                HeatProducedTodayDecimalsSensor = NumericSensor.Create(_ha, _config.HeatProducedTodayDecimalsSensor),
                HotWaterConsumedTodayIntegerSensor = NumericSensor.Create(_ha, _config.HotWaterConsumedTodayIntegerSensor),
                HotWaterConsumedTodayDecimalsSensor = NumericSensor.Create(_ha, _config.HotWaterConsumedTodayDecimalsSensor),
                HotWaterProducedTodayIntegerSensor = NumericSensor.Create(_ha, _config.HotWaterProducedTodayIntegerSensor),
                HotWaterProducedTodayDecimalsSensor = NumericSensor.Create(_ha, _config.HotWaterProducedTodayDecimalsSensor),
            };

            _smartHeatPump = await Domain.SmartHeatPump.SmartHeatPump.Create(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Something horrible happened :/");
        }
    }

    public ValueTask DisposeAsync()
    {
        _logger.LogInformation("Disposing smart heat pump");
        _smartHeatPump.Dispose();

        _logger.LogInformation("Disposed smart heat pump");

        return ValueTask.CompletedTask;
    }
}
