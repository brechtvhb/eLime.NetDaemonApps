// Use unique namespaces for your apps if you going to share with others to avoid
// conflicting names

using eLime.netDaemonApps.Config;
using eLime.NetDaemonApps.Domain.Rooms;
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
    private readonly ILogger<FlexiLights> _logger;
    private readonly FlexLightConfig _config;
    private CancellationToken _ct;
    public List<Room> Rooms { get; set; } = new();
    public FlexiLights(IHaContext ha, IScheduler scheduler, IAppConfig<FlexLightConfig> config, ILogger<FlexiLights> logger)
    {
        _ha = ha;
        _scheduler = scheduler;
        _logger = logger;
        _config = config.Value;
    }

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        _ct = cancellationToken;
        try
        {
            foreach (var roomConfig in _config.Rooms)
            {
                var room = new Room(_ha, _logger, _scheduler, roomConfig);
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
