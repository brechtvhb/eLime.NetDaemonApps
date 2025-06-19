using eLime.NetDaemonApps.Domain.EnergyManager;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using Microsoft.Extensions.Logging;

namespace eLime.NetDaemonApps.Domain.EnergyManager2.Consumers.Cooling;

public class CoolingEnergyConsumer2 : EnergyConsumer2
{
    internal sealed override EnergyConsumerMqttSensors MqttSensors { get; }
    internal sealed override CoolingEnergyConsumerHomeAssistantEntities HomeAssistant { get; }
    internal override bool IsRunning => HomeAssistant.SocketSwitch.IsOn();
    internal override double PeakLoad { get; }
    public double TargetTemperature { get; set; }
    public double MaxTemperature { get; set; }

    internal CoolingEnergyConsumer2(EnergyManagerContext context, EnergyConsumerConfiguration config)
        : base(context, config)
    {
        if (config.Cooling == null)
            throw new ArgumentException("Cooling configuration is required for CoolingEnergyConsumer2.");

        HomeAssistant = new CoolingEnergyConsumerHomeAssistantEntities(config);
        HomeAssistant.SocketSwitch.TurnedOn += Socket_TurnedOn;
        HomeAssistant.SocketSwitch.TurnedOff += Socket_TurnedOff;

        HomeAssistant.TemperatureSensor.Changed += TemperatureSensor_Changed;
        MqttSensors = new EnergyConsumerMqttSensors(config.Name, context);
        PeakLoad = config.Cooling.PeakLoad;
        TargetTemperature = config.Cooling.TargetTemperature;
        MaxTemperature = config.Cooling.MaxTemperature;
    }

    private void TemperatureSensor_Changed(object? sender, Entities.NumericSensors.NumericSensorEventArgs e)
    {
        if (e.Sensor.State <= TargetTemperature)
        {
            Stop();
            return;
        }

        State.State = GetState();
    }


    protected override void StopOnBootIfEnergyIsNoLongerNeeded()
    {
        if (IsRunning && HomeAssistant.TemperatureSensor.State <= TargetTemperature)
            Stop();
    }

    protected override EnergyConsumerState GetState()
    {
        return IsRunning switch
        {
            true => EnergyConsumerState.Running,
            false when MaximumTimeout != null && State.LastRun?.Add(MaximumTimeout.Value) < Context.Scheduler.Now => EnergyConsumerState.CriticallyNeedsEnergy,
            false when HomeAssistant.CriticallyNeededSensor != null && HomeAssistant.CriticallyNeededSensor.IsOn() => EnergyConsumerState.CriticallyNeedsEnergy,
            false when HomeAssistant.TemperatureSensor.State >= MaxTemperature => EnergyConsumerState.CriticallyNeedsEnergy,
            false when HomeAssistant.TemperatureSensor.State >= TargetTemperature => EnergyConsumerState.NeedsEnergy,
            false => EnergyConsumerState.Off
        };
    }
    public override bool CanStart()
    {
        if (State.State is EnergyConsumerState.Running or EnergyConsumerState.Off)
            return false;

        if (!IsWithinTimeWindow() && HasTimeWindow())
            return false;

        if (HomeAssistant.TemperatureSensor.State < TargetTemperature)
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

        if (HomeAssistant.TemperatureSensor.State > MaxTemperature)
            return false;

        return true;
    }

    public override bool CanForceStopOnPeakLoad()
    {
        if (MinimumRuntime != null && State.StartedAt?.Add(MinimumRuntime.Value) > Context.Scheduler.Now)
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

    private async void Socket_TurnedOn(object? sender, BinarySensorEventArgs e)
    {
        try
        {
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
        HomeAssistant.SocketSwitch.TurnedOn -= Socket_TurnedOn;
        HomeAssistant.SocketSwitch.TurnedOn -= Socket_TurnedOff;
        HomeAssistant.Dispose();
        MqttSensors.Dispose();
    }
}