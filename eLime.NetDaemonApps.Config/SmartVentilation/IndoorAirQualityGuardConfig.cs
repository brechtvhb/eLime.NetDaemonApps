namespace eLime.NetDaemonApps.Config.SmartVentilation;

public class IndoorAirQualityGuardConfig
{
    public IList<string> Co2Sensors { get; set; }
    public int Co2MediumThreshold { get; set; }
    public int Co2HighThreshold { get; set; }

    //TODO: PM2.5
}