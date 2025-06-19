using eLime.NetDaemonApps.Domain.EnergyManager;
using eLime.NetDaemonApps.Domain.EnergyManager2.Configuration;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using Microsoft.Extensions.Logging;

namespace eLime.NetDaemonApps.Domain.EnergyManager2.Consumers.Triggered;

public class TriggeredEnergyConsumer2 : EnergyConsumer2
{
    internal sealed override EnergyConsumerMqttSensors MqttSensors { get; }
    internal sealed override TriggeredEnergyConsumerHomeAssistantEntities HomeAssistant { get; }

    public string StartState { get; set; }
    public string? PausedState { get; set; }
    public string CompletedState { get; set; }
    public string? CriticalState { get; set; }
    public bool CanPause { get; set; }
    public bool ShutDownOnComplete { get; set; }

    public List<TriggeredEnergyConsumerState> States { get; set; }

    internal override bool IsRunning => (HomeAssistant.SocketSwitch == null || HomeAssistant.SocketSwitch.IsOn()) && States.Where(x => x.IsRunning).Select(x => x.Name).Contains(HomeAssistant.StateSensor.State);
    internal override double PeakLoad
    {
        get
        {
            var currentState = HomeAssistant.StateSensor.State;

            var currentStatePassed = false;
            var maxPeakLoad = 0d;

            foreach (var state in States)
            {
                if (state.Name == currentState)
                    currentStatePassed = true;

                if (currentStatePassed && state.PeakLoad > maxPeakLoad)
                    maxPeakLoad = state.PeakLoad;
            }

            return maxPeakLoad;
        }
    }

    internal TriggeredEnergyConsumer2(EnergyManagerContext context, EnergyConsumerConfiguration config)
        : base(context, config)
    {
        if (config.Triggered == null)
            throw new ArgumentException("Simple configuration is required for TriggeredEnergyConsumer2.");

        HomeAssistant = new TriggeredEnergyConsumerHomeAssistantEntities(config);

        if (HomeAssistant.SocketSwitch != null)
        {
            HomeAssistant.SocketSwitch.TurnedOn += Socket_TurnedOn;
            HomeAssistant.SocketSwitch.TurnedOff += Socket_TurnedOff;
        }
        HomeAssistant.StateSensor.StateChanged += StateSensor_StateChanged;

        StartState = config.Triggered.StartState;
        PausedState = config.Triggered.PausedState;
        CompletedState = config.Triggered.CompletedState;
        CriticalState = config.Triggered.CriticalState;

        States = config.Triggered.States;
        CanPause = config.Triggered.CanPause;
        ShutDownOnComplete = config.Triggered.ShutDownOnComplete;

        MqttSensors = new EnergyConsumerMqttSensors(config.Name, context);
    }

    private void StateSensor_StateChanged(object? sender, Entities.TextSensors.TextSensorEventArgs e)
    {
        if (HomeAssistant.SocketSwitch != null && HomeAssistant.SocketSwitch.IsOff())
            return;

        if (e.Sensor.State == StartState || e.Sensor.State == PausedState)
        {
            State.State = EnergyConsumerState.NeedsEnergy;
        }
        else if (e.Sensor.State == CriticalState)
        {
            State.State = EnergyConsumerState.CriticallyNeedsEnergy;
        }
        else if (e.Sensor.State == CompletedState)
        {
            Stop();
        }
        else if (States.Where(x => x.IsRunning).Select(x => x.Name).Contains(e.Sensor.State) && State.State != EnergyConsumerState.Running)
        {
            State.State = EnergyConsumerState.Running;
        }
    }

    protected override void StopOnBootIfEnergyIsNoLongerNeeded()
    {
        if (IsRunning && HomeAssistant.StateSensor.State == CompletedState)
            Stop();
    }

    protected override EnergyConsumerState GetState()
    {
        var desiredState = IsRunning switch
        {
            true => EnergyConsumerState.Running,
            false when (HomeAssistant.StateSensor.State == StartState || HomeAssistant.StateSensor.State == PausedState) && HomeAssistant.CriticallyNeededSensor != null && HomeAssistant.CriticallyNeededSensor.IsOn() => EnergyConsumerState.CriticallyNeedsEnergy,
            false when HomeAssistant.StateSensor.State == CriticalState && !string.IsNullOrWhiteSpace(CriticalState) => EnergyConsumerState.CriticallyNeedsEnergy,
            false when HomeAssistant.StateSensor.State == StartState || HomeAssistant.StateSensor.State == PausedState => EnergyConsumerState.NeedsEnergy,
            false => EnergyConsumerState.Off,
        };

        return desiredState;
    }

    public override bool CanStart()
    {
        if (HomeAssistant.StateSensor.State == PausedState)
            return true;

        if (State.State is EnergyConsumerState.Running or EnergyConsumerState.Off)
            return false;

        if (!IsWithinTimeWindow() && HasTimeWindow())
            return false;

        if (MinimumTimeout == null)
            return true;

        return !(State.LastRun?.Add(MinimumTimeout.Value) > Context.Scheduler.Now);
    }


    public override bool CanForceStop()
    {
        if (MinimumRuntime != null && State.StartedAt?.Add(MinimumRuntime.Value) > Context.Scheduler.Now)
            return false;

        if (HomeAssistant.CriticallyNeededSensor != null && HomeAssistant.CriticallyNeededSensor.IsOn())
            return false;

        if (!CanPause)
            return false;

        return true;
    }

    public override bool CanForceStopOnPeakLoad()
    {
        if (MinimumRuntime != null && State.StartedAt?.Add(MinimumRuntime.Value) > Context.Scheduler.Now)
            return false;

        if (!CanPause)
            return false;

        return true;
    }

    public override void TurnOn()
    {
        if (HomeAssistant.StateSensor.State == PausedState && CanPause && HomeAssistant.PauseSwitch != null)
        {
            HomeAssistant.PauseSwitch.TurnOn();
            return;
        }
        if (HomeAssistant.StateSensor.State == StartState && HomeAssistant.SocketSwitch != null && HomeAssistant.SocketSwitch.IsOff())
        {
            HomeAssistant.SocketSwitch.TurnOn();
            return;
        }

        if (HomeAssistant.StateSensor.State == StartState && HomeAssistant.StartButton != null)
            HomeAssistant.StartButton.Press();
    }

    public override void TurnOff()
    {
        if (HomeAssistant.StateSensor.State != CompletedState && CanPause && HomeAssistant.PauseSwitch != null)
        {
            Context.Logger.LogInformation($"{Name}: Pause was triggered.");
            HomeAssistant.PauseSwitch.TurnOff();
            return;
        }

        if (ShutDownOnComplete)
            HomeAssistant.SocketSwitch?.TurnOff();
    }

    private async void Socket_TurnedOn(object? sender, BinarySensorEventArgs e)
    {
        try
        {
            if (!States.Where(x => x.IsRunning).Select(x => x.Name).Contains(HomeAssistant.StateSensor.State))
                return;

            Started();
            await DebounceSaveAndPublishState();
        }
        catch (Exception ex)
        {
            Context.Logger.LogError(ex, "An error occurred while handling the socket turned on event.");
        }
    }

    private async void Socket_TurnedOff(object? sender, BinarySensorEventArgs e)
    {
        try
        {
            Stopped();
            await DebounceSaveAndPublishState();
        }
        catch (Exception ex)
        {
            Context.Logger.LogError(ex, "An error occurred while handling the socket turned off event.");
        }
    }


    public override void Dispose()
    {
        if (HomeAssistant.SocketSwitch != null)
        {
            HomeAssistant.SocketSwitch.TurnedOn -= Socket_TurnedOn;
            HomeAssistant.SocketSwitch.TurnedOn -= Socket_TurnedOff;
        }

        HomeAssistant.Dispose();
        MqttSensors.Dispose();
    }
}