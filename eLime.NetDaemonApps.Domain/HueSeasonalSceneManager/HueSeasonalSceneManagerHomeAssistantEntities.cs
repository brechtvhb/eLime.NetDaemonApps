using eLime.NetDaemonApps.Domain.Entities.BinarySensors;

namespace eLime.NetDaemonApps.Domain.HueSeasonalSceneManager;

#pragma warning disable CS8618, CS9264

public class HueZoneHomeAssistantEntities(HueZoneConfiguration config) : IDisposable
{
    internal string Zone = config.Zone;
    internal List<SeasonalSceneHomeAssistantEntities> SeasonalScenes = config.SeasonalScenes.Select(s => new SeasonalSceneHomeAssistantEntities(s)).ToList();

    public void Dispose()
    {
        foreach (var scene in SeasonalScenes)
        {
            scene.Dispose();
        }
    }
}

public class SeasonalSceneHomeAssistantEntities(SeasonalSceneConfiguration config) : IDisposable
{
    internal BinarySensor OperatingModeSensor = config.OperatingModeSensor;

    public void Dispose()
    {
        OperatingModeSensor.Dispose();
    }
}
