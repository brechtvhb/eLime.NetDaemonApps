using eLime.NetDaemonApps.Config;
using eLime.NetDaemonApps.Domain.HueSeasonalSceneManager;
using eLime.NetDaemonApps.Domain.Storage;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable CS8618, CS9264

namespace eLime.NetDaemonApps.apps.HueSeasonalSceneManager;

[NetDaemonApp(Id = "hueSeasonalSceneManager"), Focus]
public class HueSeasonalSceneManager(IHaContext ha, IScheduler scheduler, IAppConfig<HueSeasonalSceneManagerConfig> config, IFileStorage fileStorage, ILogger<HueSeasonalSceneManager> logger)
    : IAsyncInitializable, IAsyncDisposable
{
    private Domain.HueSeasonalSceneManager.HueSeasonalSceneManager _hueSeasonalSceneManager;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            var configuration = new HueSeasonalSceneManagerConfiguration(ha, logger, scheduler, fileStorage, config.Value);
            _hueSeasonalSceneManager = await Domain.HueSeasonalSceneManager.HueSeasonalSceneManager.Create(configuration);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Something horrible happened while initializing Hue Seasonal Scene Manager :/");
        }
    }

    public ValueTask DisposeAsync()
    {
        _hueSeasonalSceneManager?.Dispose();
        logger.LogInformation("Disposed Hue Seasonal Scene Manager");
        return ValueTask.CompletedTask;
    }
}
