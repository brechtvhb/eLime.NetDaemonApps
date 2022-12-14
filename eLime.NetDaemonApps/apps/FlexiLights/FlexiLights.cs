// Use unique namespaces for your apps if you going to share with others to avoid
// conflicting names

using eLime.netDaemonApps.Config;
using eLime.NetDaemonApps.Domain.Rooms;
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
    private readonly FlexLightConfig _config;
    private CancellationToken _ct;
    public List<Room> Rooms { get; set; } = new();
    public FlexiLights(IHaContext ha, IScheduler scheduler, IAppConfig<FlexLightConfig> config, IMqttEntityManager mqttEntityManager, ILogger<FlexiLights> logger)
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
            foreach (var (roomName, roomConfig) in _config.Rooms)
            {
                if (String.IsNullOrWhiteSpace(roomConfig.Name))
                    roomConfig.Name = roomName;

                var room = new Room(_ha, _logger, _scheduler, _mqttEntityManager, roomConfig, TimeSpan.FromSeconds(1));
                Rooms.Add(room);
                room.Guard();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Something horrible happened :/");
        }

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
