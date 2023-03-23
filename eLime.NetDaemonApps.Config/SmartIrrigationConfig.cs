using eLime.NetDaemonApps.Config.SmartIrrigation;

namespace eLime.NetDaemonApps.Config;

public class SmartIrrigationConfig
{
    public String PumpEntity { get; set; }
    public Int32 PumpFlowRate { get; set; }
    public String AvailableRainWaterEntity { get; set; }
    public Int32 MinimumAvailableRainWater { get; set; }
    public IDictionary<String, IrrigationZoneConfig> Zones { get; set; }
}
