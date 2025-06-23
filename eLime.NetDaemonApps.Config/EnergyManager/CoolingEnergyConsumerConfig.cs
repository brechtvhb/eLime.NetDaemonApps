namespace eLime.NetDaemonApps.Config.EnergyManager;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

public class CoolingEnergyConsumerConfig
{
    public string SocketEntity { get; set; }
    public double PeakLoad { get; set; }

    public string TemperatureSensor { get; set; }
    public double TargetTemperature { get; set; }
    public double MaxTemperature { get; set; }
}