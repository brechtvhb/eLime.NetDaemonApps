namespace eLime.NetDaemonApps.Domain.LightsRandomizer;

public class LightingZone
{
    public string Name { get; private init; }
    public List<string> AllowedScenes { get; private init; } = [];

    public static LightingZone Create(string name, List<string> allowedScenes)
    {
        return new LightingZone { Name = name, AllowedScenes = allowedScenes };
    }
}