using eLime.NetDaemonApps.Config.EnergyManager;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.Buttons;
using eLime.NetDaemonApps.Domain.Entities.TextSensors;
using NetDaemon.HassModel;
#pragma warning disable CS8618, CS9264

namespace eLime.NetDaemonApps.Domain.EnergyManager.Consumers.Triggered;

#pragma warning disable CS8601 // Possible null reference assignment.
public class TriggeredEnergyConsumerConfiguration
{
    public TriggeredEnergyConsumerConfiguration(IHaContext haContext, TriggeredEnergyConsumerConfig config)
    {
        SocketSwitch = !string.IsNullOrWhiteSpace(config.SocketEntity) ? BinarySwitch.Create(haContext, config.SocketEntity) : null;
        StartButton = !string.IsNullOrWhiteSpace(config.StartButton) ? new Button(haContext, config.StartButton) : null;
        PauseSwitch = !string.IsNullOrWhiteSpace(config.PauseSwitch) ? BinarySwitch.Create(haContext, config.PauseSwitch) : null;
        StateSensor = TextSensor.Create(haContext, config.StateSensor);
        StartState = config.StartState;
        PausedState = config.PausedState;
        CompletedState = config.CompletedState;
        CriticalState = config.CriticalState;
        CanPause = config.CanPause;
        ShutDownOnComplete = config.ShutDownOnComplete;
        States = config.States.Select(x => new TriggeredEnergyConsumerState(x)).ToList();
    }
    public BinarySwitch? SocketSwitch { get; set; }
    public Button? StartButton { get; set; }
    public BinarySwitch? PauseSwitch { get; set; } //Button once washer is Mielefied
    public TextSensor StateSensor { get; set; }
    public string StartState { get; set; }
    public string PausedState { get; set; }
    public string CompletedState { get; set; }
    public string CriticalState { get; set; }
    public bool CanPause { get; set; }
    public bool ShutDownOnComplete { get; set; }
    public List<TriggeredEnergyConsumerState> States { get; set; }
}