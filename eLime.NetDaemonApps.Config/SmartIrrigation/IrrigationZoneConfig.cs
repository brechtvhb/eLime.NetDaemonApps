namespace eLime.NetDaemonApps.Config.SmartIrrigation;

public class IrrigationZoneConfig
{
    public string Name { get; set; }

    public int FlowRate { get; set; }
    public string ValveEntity { get; set; }

    public ContainerIrrigationConfig? Container { get; set; }
    public ClassicIrrigationConfig? Irrigation { get; set; }
    public AntiFrostMistingIrrigationConfig? AntiFrostMisting { get; set; }

}