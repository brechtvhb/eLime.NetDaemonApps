namespace eLime.NetDaemonApps.Config.FlexiScreens;

public class SunProtectionConfig
{
    public string SunEntity { get; set; }
    public double? OrientationThreshold { get; set; }
    public double? ElevationThreshold { get; set; }
    public ScreenAction DesiredStateBelowElevationThreshold { get; set; } //For Living etc: Screen should go up, For sleeping rooms screen should go down
}