using eLime.NetDaemonApps.Domain.EnergyManager;
using eLime.NetDaemonApps.Domain.EnergyManager2.Configuration;
using eLime.NetDaemonApps.Domain.EnergyManager2.HomeAssistant;
using eLime.NetDaemonApps.Domain.EnergyManager2.Mqtt;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Storage;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.EnergyManager2.Consumers;

public class CoolingEnergyConsumer2 : EnergyConsumer2
{
    internal sealed override EnergyConsumerMqttSensors MqttSensors { get; }
    internal sealed override CoolingEnergyConsumerHomeAssistantEntities HomeAssistant { get; }
    internal override bool IsRunning => HomeAssistant.SocketSwitch.IsOn();
    internal override double PeakLoad { get; }
    public Double TargetTemperature { get; set; }
    public Double MaxTemperature { get; set; }

    internal CoolingEnergyConsumer2(ILogger logger, IFileStorage fileStorage, IScheduler scheduler, IMqttEntityManager mqttEntityManager, string timeZone, EnergyConsumerConfiguration config)
        : base(logger, fileStorage, scheduler, timeZone, config)
    {
        if (config.Cooling == null)
            throw new ArgumentException("Cooling configuration is required for CoolingEnergyConsumer2.");

        HomeAssistant = new CoolingEnergyConsumerHomeAssistantEntities(config);
        HomeAssistant.SocketSwitch.TurnedOn += Socket_TurnedOn;
        HomeAssistant.SocketSwitch.TurnedOff += Socket_TurnedOff;

        MqttSensors = new EnergyConsumerMqttSensors(config.Name, mqttEntityManager);
        PeakLoad = config.Cooling.PeakLoad;
        TargetTemperature = config.Cooling.TargetTemperature;
        MaxTemperature = config.Cooling.MaxTemperature;
    }

    protected override EnergyConsumerState GetDesiredState(DateTimeOffset? now)
    {
        return IsRunning switch
        {
            true when HomeAssistant.TemperatureSensor.State < TargetTemperature => EnergyConsumerState.Off,
            true => EnergyConsumerState.Running,
            false when MaximumTimeout != null && State.LastRun?.Add(MaximumTimeout.Value) < now => EnergyConsumerState.CriticallyNeedsEnergy,
            false when HomeAssistant.CriticallyNeededSensor != null && HomeAssistant.CriticallyNeededSensor.IsOn() => EnergyConsumerState.CriticallyNeedsEnergy,
            false when HomeAssistant.TemperatureSensor.State >= MaxTemperature => EnergyConsumerState.CriticallyNeedsEnergy,
            false when HomeAssistant.TemperatureSensor.State >= TargetTemperature => EnergyConsumerState.NeedsEnergy,
            false => EnergyConsumerState.Off
        };
    }
    public override bool CanStart(DateTimeOffset now)
    {
        if (State.State is EnergyConsumerState.Running or EnergyConsumerState.Off)
            return false;

        if (!IsWithinTimeWindow(now) && HasTimeWindow())
            return false;

        if (HomeAssistant.TemperatureSensor.State < TargetTemperature)
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

        if (HomeAssistant.TemperatureSensor.State > MaxTemperature)
            return false;

        return true;
    }

    public override bool CanForceStopOnPeakLoad(DateTimeOffset now)
    {
        if (MinimumRuntime != null && State.StartedAt?.Add(MinimumRuntime.Value) > now)
            return false;

        return true;
    }

    public override void TurnOn()
    {
        HomeAssistant.SocketSwitch.TurnOn();
    }

    public override void TurnOff()
    {
        HomeAssistant.SocketSwitch.TurnOff();
    }

    private void Socket_TurnedOn(object? sender, BinarySensorEventArgs e)
    {
        CheckDesiredState(new EnergyConsumer2StartedEvent(this, EnergyConsumerState.Running));
    }

    private void Socket_TurnedOff(object? sender, BinarySensorEventArgs e)
    {
        CheckDesiredState(new EnergyConsumer2StoppedEvent(this, EnergyConsumerState.Off));
    }
    public override void Dispose()
    {
        HomeAssistant.SocketSwitch.TurnedOn -= Socket_TurnedOn;
        HomeAssistant.SocketSwitch.TurnedOn -= Socket_TurnedOff;
        HomeAssistant.Dispose();
        MqttSensors.Dispose();
    }
}