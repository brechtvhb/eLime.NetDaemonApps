namespace eLime.NetDaemonApps.Config.EnergyManager;

public class CoolingEnergyConsumerConfig
{
    public string SocketEntity { get; set; }
    public double PeakLoad { get; set; }

    public string TemperatureSensor { get; set; }
    public double TargetTemperature { get; set; }
    public double MaxTemperature { get; set; }
}