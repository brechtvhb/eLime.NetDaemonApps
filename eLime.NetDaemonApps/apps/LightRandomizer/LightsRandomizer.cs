using eLime.NetDaemonApps.Config;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Domain.LightsRandomizer;
using eLime.NetDaemonApps.Domain.Storage;
using MediatR;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.Extensions.Scheduler;
using System.Linq;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;

namespace eLime.NetDaemonApps.apps.LightRandomizer;


[NetDaemonApp(Id = "lightsrandomizer")]
public class LightsRandomizer : IAsyncInitializable, IAsyncDisposable
{
    private readonly IHaContext _ha;
    private readonly IScheduler _scheduler;
    private readonly IMqttEntityManager _mqttEntityManager;
    private readonly IMediator _mediator;
    private readonly ILogger<LightsRandomizer> _logger;
    private readonly IFileStorage _fileStorage;
    private readonly LightsRandomizerConfig _config;
    private CancellationToken _ct;
    private Domain.LightsRandomizer.LightRandomizer lightRandomizer;
    BinarySensor lightingAllowedSensor;

    private IDisposable? SelectLightsScheduledTask { get; set; }
    public LightsRandomizer(IHaContext ha, IScheduler scheduler, IAppConfig<LightsRandomizerConfig> config,
        IMqttEntityManager mqttEntityManager, IMediator mediator, ILogger<LightsRandomizer> logger, IFileStorage fileStorage)
    {
        _ha = ha;
        _scheduler = scheduler;
        _mqttEntityManager = mqttEntityManager;
        _mediator = mediator;
        _logger = logger;
        _fileStorage = fileStorage;
        _config = config.Value;
    }

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        _ct = cancellationToken;

        try
        {
            var storage = _fileStorage.Get<LightsRandomizerStorage>("LightsRandomizer", "LightsRandomizer");

            var lightingZones = _config.Zones.Select(x => LightingZone.Create(x.Name, x.Scenes)).ToList();
            lightRandomizer = Domain.LightsRandomizer.LightRandomizer.Create(_mediator, lightingZones, _config.AmountOfZonesToLight, storage);

            lightingAllowedSensor = BinarySensor.Create(_ha, _config.LightingAllowedSensor);
            lightingAllowedSensor.TurnedOn += LightingAllowedSensor_TurnedOn;
            lightingAllowedSensor.TurnedOff += LightingAllowedSensor_TurnedOff;

            var startTime = new TimeOnly(00, 02);
            var startFrom = startTime.GetUtcDateTimeFromLocalTimeOnly(_scheduler.Now.DateTime, "Europe/Brussels").AddDays(1);

            _logger.LogInformation($"Will select new lights to turn on at: {startFrom:O}");

            SelectLightsScheduledTask = _scheduler.RunEvery(TimeSpan.FromDays(1), startFrom, () =>
            {
                lightRandomizer.SelectZonesForToday(_mediator);
            });

            if (storage?.SelectedZones == null || storage.SelectedZones.Count == 0)
                lightRandomizer.SelectZonesForToday(_mediator);

            if (lightingAllowedSensor.IsOn())
                lightRandomizer.TurnOnLights(_mediator);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Something horrible happened :/");
        }

        return Task.CompletedTask;
    }

    private void LightingAllowedSensor_TurnedOn(object? sender, BinarySensorEventArgs e)
    {
        lightRandomizer.TurnOnLights(_mediator);
    }
    private void LightingAllowedSensor_TurnedOff(object? sender, BinarySensorEventArgs e)
    {
        lightRandomizer.TurnOffLights(_mediator);
    }

    public ValueTask DisposeAsync()
    {
        _logger.LogInformation("Disposing LightsRandomizer");

        SelectLightsScheduledTask?.Dispose();
        lightingAllowedSensor.TurnedOn -= LightingAllowedSensor_TurnedOn;
        lightingAllowedSensor.TurnedOff -= LightingAllowedSensor_TurnedOff;
        lightingAllowedSensor.Dispose();

        GC.SuppressFinalize(this);

        return ValueTask.CompletedTask;
    }
}
