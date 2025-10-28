using eLime.NetDaemonApps.Domain.Storage;
using HueApi;
using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.HueSeasonalSceneManager;

public class HueZoneContext
{
    public ILogger Logger { get; }
    public IScheduler Scheduler { get; }
    public IFileStorage FileStorage { get; }
    public LocalHueApi HueClient { get; }
    public HueZoneConfiguration Configuration { get; }

    public HueZoneContext(ILogger logger, IScheduler scheduler, IFileStorage fileStorage, LocalHueApi hueClient, HueZoneConfiguration configuration)
    {
        Logger = logger;
        Scheduler = scheduler;
        FileStorage = fileStorage;
        HueClient = hueClient;
        Configuration = configuration;
    }
}
