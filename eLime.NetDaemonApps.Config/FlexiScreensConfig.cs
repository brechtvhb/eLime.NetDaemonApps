using eLime.NetDaemonApps.Config.FlexiScreens;

namespace eLime.NetDaemonApps.Config;

public class FlexiScreensConfig
{
    public String NetDaemonUserId { get; set; }
    public IDictionary<String, FlexiScreenConfig> Screens { get; set; }
}
