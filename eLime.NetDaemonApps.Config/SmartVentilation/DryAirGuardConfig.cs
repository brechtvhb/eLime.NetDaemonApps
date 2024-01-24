namespace eLime.NetDaemonApps.Config.SmartVentilation;

public class DryAirGuardConfig
{
    public IList<string> HumiditySensors { get; set; }
    public int HumidityLowThreshold { get; set; }

    public string OutdoorTemperatureSensor { get; set; }
    public int MaxOutdoorTemperature { get; set; }
}