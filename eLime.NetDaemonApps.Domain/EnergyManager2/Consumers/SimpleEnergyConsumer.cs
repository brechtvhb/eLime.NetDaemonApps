using eLime.NetDaemonApps.Domain.EnergyManager;
using eLime.NetDaemonApps.Domain.EnergyManager2.Configuration;
using eLime.NetDaemonApps.Domain.EnergyManager2.HomeAssistant;
using eLime.NetDaemonApps.Domain.EnergyManager2.Mqtt;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Storage;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;

namespace eLime.NetDaemonApps.Domain.EnergyManager2.Consumers;

public class SimpleEnergyConsumer2 : EnergyConsumer2
{
    internal SimpleEnergyConsumer2(ILogger logger, IFileStorage fileStorage, IMqttEntityManager mqttEntityManager, string timeZone, EnergyConsumerConfiguration config)
        : base(logger, fileStorage, timeZone, config)
    {
        if (config.Simple == null)
            throw new ArgumentException("Simple configuration is required for SimpleEnergyConsumer2.");

        HomeAssistant = new SimpleEnergyConsumerHomeAssistantEntities(config);
        HomeAssistant.SocketSwitch.TurnedOn += Socket_TurnedOn;
        HomeAssistant.SocketSwitch.TurnedOff += Socket_TurnedOff;

        MqttSensors = new EnergyConsumerMqttSensors(config.Name, mqttEntityManager);
        PeakLoad = config.Simple.PeakLoad;
    }

    internal sealed override EnergyConsumerMqttSensors MqttSensors { get; }
    internal sealed override SimpleEnergyConsumerHomeAssistantEntities HomeAssistant { get; }

    internal override bool IsRunning => HomeAssistant.SocketSwitch.IsOn();
    internal override double PeakLoad { get; }


    protected override EnergyConsumerState GetDesiredState(DateTimeOffset? now)
    {
        return IsRunning switch
        {
            true => EnergyConsumerState.Running,
            false when MaximumTimeout != null && State.LastRun?.Add(MaximumTimeout.Value) < now => EnergyConsumerState.CriticallyNeedsEnergy,
            false when HomeAssistant.CriticallyNeededSensor != null && HomeAssistant.CriticallyNeededSensor.IsOn() => EnergyConsumerState.CriticallyNeedsEnergy,
            false => EnergyConsumerState.NeedsEnergy,
        };
    }

    public override bool CanStart(DateTimeOffset now)
    {
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

        return true;
    }

    public override bool CanForceStopOnPeakLoad(DateTimeOffset now)
    {
        //Do not care about minimum runtime if peak load hits, happens only several times a month ...
        //if (MinimumRuntime != null && StartedAt?.Add(MinimumRuntime.Value) > now)
        //    return false;

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

    public override void DisposeInternal()
    {
        HomeAssistant.SocketSwitch.TurnedOn -= Socket_TurnedOn;
        HomeAssistant.SocketSwitch.TurnedOn -= Socket_TurnedOff;
        HomeAssistant.SocketSwitch.Dispose();
    }

    private void Socket_TurnedOn(object? sender, BinarySensorEventArgs e)
    {
        CheckDesiredState(new EnergyConsumer2StartedEvent(this, EnergyConsumerState.Running));
    }

    private void Socket_TurnedOff(object? sender, BinarySensorEventArgs e)
    {
        CheckDesiredState(new EnergyConsumer2StoppedEvent(this, EnergyConsumerState.Off));
    }
}