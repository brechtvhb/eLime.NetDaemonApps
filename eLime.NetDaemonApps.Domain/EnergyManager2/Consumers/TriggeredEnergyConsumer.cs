using eLime.NetDaemonApps.Domain.EnergyManager;
using eLime.NetDaemonApps.Domain.EnergyManager2.Configuration;
using eLime.NetDaemonApps.Domain.EnergyManager2.HomeAssistant;
using eLime.NetDaemonApps.Domain.EnergyManager2.Mqtt;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.TextSensors;
using eLime.NetDaemonApps.Domain.Storage;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.EnergyManager2.Consumers;

public class TriggeredEnergyConsumer2 : EnergyConsumer2
{
    internal sealed override EnergyConsumerMqttSensors MqttSensors { get; }
    internal sealed override TriggeredEnergyConsumerHomeAssistantEntities HomeAssistant { get; }

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

    public string StartState { get; set; }
    public string? PausedState { get; set; }
    public string CompletedState { get; set; }
    public string? CriticalState { get; set; }
    public bool CanPause { get; set; }
    public bool ShutDownOnComplete { get; set; }

    public List<TriggeredEnergyConsumerState> States { get; set; }

    internal TriggeredEnergyConsumer2(ILogger logger, IFileStorage fileStorage, IScheduler scheduler, IMqttEntityManager mqttEntityManager, string timeZone, EnergyConsumerConfiguration config)
        : base(logger, fileStorage, scheduler, timeZone, config)
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

        MqttSensors = new EnergyConsumerMqttSensors(config.Name, mqttEntityManager);
    }

    protected override EnergyConsumerState GetDesiredState(DateTimeOffset? now)
    {
        var desiredState = IsRunning switch
        {
            true when HomeAssistant.StateSensor.State == CompletedState => EnergyConsumerState.Off,
            true when HomeAssistant.StateSensor.State == PausedState => EnergyConsumerState.NeedsEnergy,
            true => EnergyConsumerState.Running,
            false when (HomeAssistant.StateSensor.State == StartState || HomeAssistant.StateSensor.State == PausedState) && HomeAssistant.CriticallyNeededSensor != null && HomeAssistant.CriticallyNeededSensor.IsOn() => EnergyConsumerState.CriticallyNeedsEnergy,
            false when HomeAssistant.StateSensor.State == CriticalState && !string.IsNullOrWhiteSpace(CriticalState) => EnergyConsumerState.CriticallyNeedsEnergy,
            false when (HomeAssistant.StateSensor.State == StartState || HomeAssistant.StateSensor.State == PausedState) => EnergyConsumerState.NeedsEnergy,
            false => EnergyConsumerState.Off,
        };

        return desiredState;
    }

    public override bool CanStart(DateTimeOffset now)
    {
        if (HomeAssistant.StateSensor.State == PausedState)
            return true;

        if (State.State is EnergyConsumerState.Running or EnergyConsumerState.Off)
            return false;

        if (!IsWithinTimeWindow(now) && HasTimeWindow())
            return false;

        if (MinimumTimeout == null)
            return true;

        return !(State.LastRun?.Add(MinimumTimeout.Value) > now);
    }


    public override bool CanForceStop(DateTimeOffset now)
    {
        if (MinimumRuntime != null && State.StartedAt?.Add(MinimumRuntime.Value) > now)
            return false;

        if (HomeAssistant.CriticallyNeededSensor != null && HomeAssistant.CriticallyNeededSensor.IsOn())
            return false;

        if (!CanPause)
            return false;

        return true;
    }

    public override bool CanForceStopOnPeakLoad(DateTimeOffset now)
    {
        if (MinimumRuntime != null && State.StartedAt?.Add(MinimumRuntime.Value) > now)
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
            Logger.LogInformation($"{Name}: Pause was triggered.");
            HomeAssistant.PauseSwitch.TurnOff();
            return;
        }

        if (ShutDownOnComplete)
            HomeAssistant.SocketSwitch?.TurnOff();
    }

    private void Socket_TurnedOn(object? sender, BinarySensorEventArgs e)
    {
        if (States.Where(x => x.IsRunning).Select(x => x.Name).Contains(HomeAssistant.StateSensor.State))
            CheckDesiredState(new EnergyConsumer2StartedEvent(this, EnergyConsumerState.Running));
    }

    private void Socket_TurnedOff(object? sender, BinarySensorEventArgs e)
    {
        CheckDesiredState(new EnergyConsumer2StoppedEvent(this, EnergyConsumerState.Off));
    }


    private void StateSensor_StateChanged(object? sender, TextSensorEventArgs e)
    {
        if (HomeAssistant.SocketSwitch != null && HomeAssistant.SocketSwitch.IsOff())
            return;

        if (e.Sensor.State == StartState || e.Sensor.State == PausedState)
        {
            CheckDesiredState(new EnergyConsumer2StartCommand(this, EnergyConsumerState.NeedsEnergy));
        }
        else if (e.Sensor.State == CriticalState)
        {
            CheckDesiredState(new EnergyConsumer2StartCommand(this, EnergyConsumerState.CriticallyNeedsEnergy));
        }
        else if (e.Sensor.State == CompletedState)
        {
            Logger.LogInformation($"{Name}: Sensor state ({e.Sensor.State}) = completed state ({CompletedState})");
            CheckDesiredState(new EnergyConsumer2StopCommand(this, EnergyConsumerState.Off));
        }
        else if (States.Where(x => x.IsRunning).Select(x => x.Name).Contains(HomeAssistant.StateSensor.State) && State.State != EnergyConsumerState.Running)
        {
            CheckDesiredState(new EnergyConsumer2StartCommand(this, EnergyConsumerState.Running));
        }
    }

    public override void Dispose()
    {
        if (HomeAssistant.SocketSwitch != null)
        {
            HomeAssistant.SocketSwitch.TurnedOn -= Socket_TurnedOn;
            HomeAssistant.SocketSwitch.TurnedOn -= Socket_TurnedOff;
        }

        HomeAssistant.StateSensor.StateChanged -= StateSensor_StateChanged;

        HomeAssistant.Dispose();
        MqttSensors.Dispose();
    }
}