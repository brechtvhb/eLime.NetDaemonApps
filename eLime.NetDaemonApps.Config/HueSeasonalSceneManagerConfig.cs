#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
namespace eLime.NetDaemonApps.Config;

public class HueSeasonalSceneManagerConfig
{
    public List<BridgeConfig> Bridges { get; set; } = [];
}

public class BridgeConfig
{
    public string Name { get; set; }
    public string IpAddress { get; set; }
    public List<HueZoneConfig> Zones { get; set; } = [];
}


public class HueZoneConfig
{
    public string Zone { get; set; } //Informative only
    public string AllDaySceneId { get; set; }

    public SceneTimeSlotConfig FallbackScenes { get; set; }
    public List<SeasonalSceneConfig> SeasonalScenes { get; set; } = [];
}


public class SceneTimeSlotConfig
{
    public string? Timeslot1 { get; set; } //7:00
    public string? Timeslot2 { get; set; } //10:00
    public string? Timeslot3 { get; set; } //Sunset
    public string? Timeslot4 { get; set; } //20:00
    public string? Timeslot5 { get; set; } //22:30
    public string? Timeslot6 { get; set; } //Nightly
}

public class SeasonalSceneConfig
{
    public string Festivity { get; set; } //Informative only
    public string OperatingModeSensor { get; set; }
    public SceneTimeSlotConfig Scenes { get; set; }
}