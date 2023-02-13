namespace eLime.NetDaemonApps.Config.SmartWasher;

public class SmartWasherConfig
{
    public string Name { get; set; }
    public bool? Enabled { get; set; }

    public string PowerSensor { get; set; }
    public string PowerSocket { get; set; }
}
