namespace eLime.NetDaemonApps.Config.EnergyManager;

public class TriggeredEnergyConsumerConfig
{
    public String SocketEntity { get; set; }
    public String StateSensor { get; set; }
    public String StartState { get; set; }
    public String CompletedState { get; set; }
    public String CriticalState { get; set; }
    public Boolean CanForceShutdown { get; set; }
    public Boolean ShutDownOnComplete { get; set; }
    public List<TriggeredStatePeakLoad> PeakLoads { get; set; }

}

public class TriggeredStatePeakLoad
{
    public String State { get; set; }
    public Double PeakLoad { get; set; }
}