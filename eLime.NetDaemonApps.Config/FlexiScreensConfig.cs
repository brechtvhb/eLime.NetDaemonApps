using eLime.NetDaemonApps.Config.FlexiLights;
using eLime.NetDaemonApps.Config.FlexiScreens;

namespace eLime.NetDaemonApps.Config;

public class FlexiScreensConfig
{
    public IDictionary<String, FlexiScreenConfig> Screens { get; set; }
}
