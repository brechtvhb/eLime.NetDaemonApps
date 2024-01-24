namespace eLime.NetDaemonApps.Config.SmartVentilation;

public class BathroomAirQualityGuardConfig
{
    public IList<string> HumiditySensors { get; set; }
    public int HumidityMediumThreshold { get; set; }
    public int HumidityHighThreshold { get; set; }
}