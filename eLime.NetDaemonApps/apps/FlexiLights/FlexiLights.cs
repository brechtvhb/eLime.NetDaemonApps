using eLime.netDaemonApps.Config;
using eLime.NetDaemonApps.Domain.FlexiScenes.Rooms;
using eLime.NetDaemonApps.Domain.Storage;
using NetDaemon.Extensions.MqttEntityManager;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;

namespace eLime.NetDaemonApps.apps.FlexiLights;

[NetDaemonApp(Id = "flexilights")]
public class FlexiLights : IAsyncInitializable, IAsyncDisposable
{
    private readonly IHaContext _ha;
    private readonly IScheduler _scheduler;
    private readonly IMqttEntityManager _mqttEntityManager;
    private readonly ILogger<FlexiLights> _logger;
    private readonly IFileStorage _fileStorage;
    private readonly FlexLightConfig _config;
    private CancellationToken _ct;
    public List<Room> Rooms { get; set; } = new();
    public FlexiLights(IHaContext ha, IScheduler scheduler, IAppConfig<FlexLightConfig> config, IMqttEntityManager mqttEntityManager, ILogger<FlexiLights> logger, IFileStorage fileStorage)
    {
        _ha = ha;
        _scheduler = scheduler;
        _mqttEntityManager = mqttEntityManager;
        _logger = logger;
        _fileStorage = fileStorage;
        _config = config.Value;
    }

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        _ct = cancellationToken;
        try
        {
            foreach (var (roomName, roomConfig) in _config.Rooms)
            {
                if (String.IsNullOrWhiteSpace(roomConfig.Name))
                    roomConfig.Name = roomName;

                var room = new Room(_ha, _logger, _scheduler, _mqttEntityManager, _fileStorage, roomConfig, TimeSpan.FromSeconds(1));
                Rooms.Add(room);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Something horrible happened :/");
        }

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _logger.LogInformation("Disposing Flexi lights");

        foreach (var room in Rooms)
            await room.DisposeAsync();

        Rooms.Clear();
        GC.SuppressFinalize(this);
    }
}
