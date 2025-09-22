namespace eLime.NetDaemonApps.Config.EnergyManager;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

public class TriggeredEnergyConsumerConfig
{
    public string SocketEntity { get; set; }
    public string? StartButton { get; set; }
    public string? PauseButton { get; set; }
    public string StateSensor { get; set; }
    public string StartState { get; set; }
    public string? PausedState { get; set; }
    public string CompletedState { get; set; }
    public string? CriticalState { get; set; }
    public bool CanPause { get; set; }
    public bool ShutDownOnComplete { get; set; }
    public List<TriggeredEnergyConsumerStateConfig> States { get; set; }

}

public class TriggeredEnergyConsumerStateConfig
{
    public string Name { get; set; }
    public double PeakLoad { get; set; }
    public bool IsRunning { get; set; }
}