using eLime.NetDaemonApps.Config;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Storage;
using Microsoft.Extensions.Logging;
using NetDaemon.HassModel;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.HueSeasonalSceneManager;

public class HueSeasonalSceneManagerConfiguration
{
    public HueSeasonalSceneManagerContext Context { get; private init; }
    public List<HueZoneConfiguration> Zones { get; private init; }

    public HueSeasonalSceneManagerConfiguration(IHaContext haContext, ILogger logger, IScheduler scheduler, IFileStorage fileStorage, BridgeConfig config)
    {
        Context = new HueSeasonalSceneManagerContext(haContext, logger, scheduler, fileStorage, config.IpAddress, config.Zones);

        Zones = config.Zones.Select(scene => new HueZoneConfiguration(haContext, scene)).ToList();
    }
}

public class HueZoneConfiguration
{
    public string Zone { get; private init; }
    public Guid AllDaySceneId { get; private init; }
    public SceneTimeSlotConfig FallbackScenes { get; private init; }
    public List<SeasonalSceneConfiguration> SeasonalScenes { get; private init; }

    public HueZoneConfiguration(IHaContext haContext, HueZoneConfig config)
    {
        Zone = config.Zone;
        AllDaySceneId = Guid.Parse(config.AllDaySceneId);
        FallbackScenes = config.FallbackScenes;
        SeasonalScenes = config.SeasonalScenes.Select(s => new SeasonalSceneConfiguration(haContext, s)).ToList();
    }
}

public class SeasonalSceneConfiguration
{
    public string Festivity { get; private init; }
    public BinarySensor OperatingModeSensor { get; private init; }
    public SceneTimeSlotConfig Scenes { get; private init; }

    public SeasonalSceneConfiguration(IHaContext haContext, SeasonalSceneConfig config)
    {
        Festivity = config.Festivity;
        OperatingModeSensor = BinarySensor.Create(haContext, config.OperatingModeSensor);
        Scenes = config.Scenes;
    }
}
