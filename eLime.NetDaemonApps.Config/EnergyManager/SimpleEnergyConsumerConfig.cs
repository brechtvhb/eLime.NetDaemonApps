namespace eLime.NetDaemonApps.Config.EnergyManager;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

public class SimpleEnergyConsumerConfig
{
    public string SocketEntity { get; set; }
    public double PeakLoad { get; set; }
}