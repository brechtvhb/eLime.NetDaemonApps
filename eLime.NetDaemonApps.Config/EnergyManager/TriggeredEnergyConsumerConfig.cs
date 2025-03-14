namespace eLime.NetDaemonApps.Config.EnergyManager;

public class TriggeredEnergyConsumerConfig
{
    public string SocketEntity { get; set; }
    public string StartButton { get; set; }
    public string PauseSwitch { get; set; }
    public string StateSensor { get; set; }
    public string StartState { get; set; }
    public string PausedState { get; set; }
    public string CompletedState { get; set; }
    public string CriticalState { get; set; }
    public bool CanPause { get; set; }
    public bool ShutDownOnComplete { get; set; }
    public List<State> States { get; set; } = [];

}

public class State
{
    public string Name { get; set; }
    public double PeakLoad { get; set; }
    public bool IsRunning { get; set; }
}