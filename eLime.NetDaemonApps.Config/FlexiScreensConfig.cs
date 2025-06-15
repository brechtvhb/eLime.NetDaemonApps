using eLime.NetDaemonApps.Config.FlexiScreens;

namespace eLime.NetDaemonApps.Config;

public class FlexiScreensConfig
{
    public string NetDaemonUserId { get; set; }
    public IDictionary<string, FlexiScreenConfig> Screens { get; set; }
}
