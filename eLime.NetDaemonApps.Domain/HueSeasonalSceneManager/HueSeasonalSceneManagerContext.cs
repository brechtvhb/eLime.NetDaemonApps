using eLime.NetDaemonApps.Config;
using eLime.NetDaemonApps.Domain.Storage;
using Microsoft.Extensions.Logging;
using NetDaemon.HassModel;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.HueSeasonalSceneManager;

public class HueSeasonalSceneManagerContext
{
    public IHaContext HaContext { get; }
    public ILogger Logger { get; }
    public IScheduler Scheduler { get; }
    public IFileStorage FileStorage { get; }
    public string BridgeIpAddress { get; }
    public List<HueZoneConfig> AllDayScenes { get; }

    public HueSeasonalSceneManagerContext(IHaContext haContext, ILogger logger, IScheduler scheduler, IFileStorage fileStorage, string bridgeIpAddress, List<HueZoneConfig> allDayScenes)
    {
        HaContext = haContext;
        Logger = logger;
        Scheduler = scheduler;
        FileStorage = fileStorage;
        BridgeIpAddress = bridgeIpAddress;
        AllDayScenes = allDayScenes;
    }
}
