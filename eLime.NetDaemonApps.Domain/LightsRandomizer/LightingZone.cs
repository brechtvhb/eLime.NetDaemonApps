namespace eLime.NetDaemonApps.Domain.LightsRandomizer;

public class LightingZone
{
    public String Name { get; private init; }
    public List<String> AllowedScenes { get; private init; } = [];

    public static LightingZone Create(String name, List<String> allowedScenes)
    {
        return new LightingZone { Name = name, AllowedScenes = allowedScenes };
    }
}