namespace eLime.NetDaemonApps.Config.SmartIrrigation;

public class ContainerIrrigationConfig
{
    public string VolumeEntity { get; set; }
    public int CriticalVolume { get; set; }
    public int LowVolume { get; set; }
    public int TargetVolume { get; set; }
    public string OverFlowEntity { get; set; }
}