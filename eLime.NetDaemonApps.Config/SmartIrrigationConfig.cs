using eLime.NetDaemonApps.Config.SmartIrrigation;

namespace eLime.NetDaemonApps.Config;

public class SmartIrrigationConfig
{
    public string PumpSocketEntity { get; set; }
    public int PumpFlowRate { get; set; }
    public string AvailableRainWaterEntity { get; set; }
    public int MinimumAvailableRainWater { get; set; }
    public string WeatherEntity { get; set; }
    public double? RainPredictionLiters { get; set; }
    public int? RainPredictionDays { get; set; }
    public string PhoneToNotify { get; set; }
    public List<IrrigationZoneConfig> Zones { get; set; }
}
