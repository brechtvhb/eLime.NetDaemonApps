using eLime.NetDaemonApps.Config.SmartIrrigation;

namespace eLime.NetDaemonApps.Config;

public class SmartIrrigationConfig
{
    public String PumpSocketEntity { get; set; }
    public Int32 PumpFlowRate { get; set; }
    public String AvailableRainWaterEntity { get; set; }
    public Int32 MinimumAvailableRainWater { get; set; }
    public String WeatherEntity { get; set; }
    public double? RainPredictionLiters { get; set; }
    public int? RainPredictionDays { get; set; }
    public List<IrrigationZoneConfig> Zones { get; set; }
}
