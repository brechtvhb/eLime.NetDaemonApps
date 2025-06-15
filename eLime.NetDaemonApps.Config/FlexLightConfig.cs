using eLime.NetDaemonApps.Config.FlexiLights;

namespace eLime.netDaemonApps.Config;

public class FlexLightConfig
{
    public IDictionary<string, RoomConfig> Rooms { get; set; }
}
