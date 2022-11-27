using eLime.NetDaemonApps.Config.FlexiLights;

namespace eLime.netDaemonApps.Config;

public class FlexLightConfig
{
    public IEnumerable<RoomConfig> Rooms { get; set; }
}
