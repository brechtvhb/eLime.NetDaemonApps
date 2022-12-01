using eLime.NetDaemonApps.Config.FlexiLights;

namespace eLime.netDaemonApps.Config;

public class FlexLightConfig
{
    public IDictionary<String, RoomConfig> Rooms { get; set; }
}
