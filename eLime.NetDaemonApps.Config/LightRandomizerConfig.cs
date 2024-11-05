namespace eLime.NetDaemonApps.Config;

public class LightsRandomizerConfig
{
    public string LightingAllowedSensor { get; set; }
    public int AmountOfZonesToLight { get; set; }
    public List<LightingZoneConfig> Zones { get; set; } = [];
}

public class LightingZoneConfig
{
    public String Name { get; set; }
    public List<String> Scenes { get; set; }
}