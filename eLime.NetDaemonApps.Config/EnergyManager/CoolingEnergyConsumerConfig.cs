namespace eLime.NetDaemonApps.Config.EnergyManager;

public class CoolingEnergyConsumerConfig
{
    public String SocketEntity { get; set; }
    public Double PeakLoad { get; set; }

    public String TemperatureSensor { get; set; }
    public Double TargetTemperature { get; set; }
    public Double SwitchOnTemperature { get; set; }
    public Double MaxTemperature { get; set; }
}