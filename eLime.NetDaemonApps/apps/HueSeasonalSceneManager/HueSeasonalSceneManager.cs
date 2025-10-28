using eLime.NetDaemonApps.Config;
using eLime.NetDaemonApps.Domain.HueSeasonalSceneManager;
using eLime.NetDaemonApps.Domain.Storage;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable CS8618, CS9264

namespace eLime.NetDaemonApps.apps.HueSeasonalSceneManager;

[NetDaemonApp(Id = "hueSeasonalSceneManager"), Focus]
public class HueSeasonalSceneManager(IHaContext ha, IScheduler scheduler, IAppConfig<HueSeasonalSceneManagerConfig> config, IFileStorage fileStorage, ILogger<HueSeasonalSceneManager> logger)
    : IAsyncInitializable, IAsyncDisposable
{
    private readonly List<Domain.HueSeasonalSceneManager.HueSeasonalSceneManager> _hueSeasonalSceneManagers = [];

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            foreach (var bridge in config.Value.Bridges)
            {
                var configuration = new HueSeasonalSceneManagerConfiguration(ha, logger, scheduler, fileStorage, bridge);
                var hueSeasonalSceneManager = await Domain.HueSeasonalSceneManager.HueSeasonalSceneManager.Create(bridge.Name, configuration);
                _hueSeasonalSceneManagers.Add(hueSeasonalSceneManager);
            }

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Something horrible happened while initializing Hue shed Seasonal Scene Manager :/");
        }
    }

    public ValueTask DisposeAsync()
    {
        foreach (var hueSeasonalSceneManager in _hueSeasonalSceneManagers)
            hueSeasonalSceneManager.Dispose();

        logger.LogInformation("Disposed Hue shed Seasonal Scene Manager");
        return ValueTask.CompletedTask;
    }
}
