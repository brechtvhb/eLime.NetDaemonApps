// Use unique namespaces for your apps if you going to share with others to avoid
// conflicting names

using FlexiLights.Config;
using FlexiLights.Data.Rooms;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FlexiLights.apps.HassModel.LightOnMovement;

[NetDaemonApp(Id = "flexilights")]
public class FlexiLights : IAsyncInitializable, IAsyncDisposable
{
    private readonly IHaContext _ha;
    private readonly ILogger<FlexiLights> _logger;
    private readonly FlexLightConfig _config;
    private CancellationToken _ct;
    public List<Room> Rooms { get; set; } = new();
    public FlexiLights(IHaContext ha, IAppConfig<FlexLightConfig> config, ILogger<FlexiLights> logger)
    {
        _ha = ha;
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
                var room = Room.Create(_ha, _logger, roomConfig);
                Task.Run(() => room.Guard(cancellationToken));
                Rooms.Add(room);
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
