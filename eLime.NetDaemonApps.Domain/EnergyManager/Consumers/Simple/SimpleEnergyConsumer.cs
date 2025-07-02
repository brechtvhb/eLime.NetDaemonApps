using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using Microsoft.Extensions.Logging;

namespace eLime.NetDaemonApps.Domain.EnergyManager.Consumers.Simple;

public class SimpleEnergyConsumer : EnergyConsumer
{
    internal sealed override EnergyConsumerMqttSensors MqttSensors { get; }
    internal sealed override SimpleEnergyConsumerHomeAssistantEntities HomeAssistant { get; }

    internal override bool IsRunning => HomeAssistant.SocketSwitch.IsOn();
    internal override double PeakLoad { get; }

    internal SimpleEnergyConsumer(EnergyManagerContext context, EnergyConsumerConfiguration config)
        : base(context, config)
    {
        if (config.Simple == null)
            throw new ArgumentException("Simple configuration is required for SimpleEnergyConsumer.");

        HomeAssistant = new SimpleEnergyConsumerHomeAssistantEntities(config);
        HomeAssistant.SocketSwitch.TurnedOn += Socket_TurnedOn;
        HomeAssistant.SocketSwitch.TurnedOff += Socket_TurnedOff;

        MqttSensors = new EnergyConsumerMqttSensors(config.Name, context);
        PeakLoad = config.Simple.PeakLoad;
    }

    protected override void StopOnBootIfEnergyIsNoLongerNeeded()
    {

    }

    protected override EnergyConsumerState GetState()
    {
        return IsRunning switch
        {
            true => EnergyConsumerState.Running,
            false when MaximumTimeout != null && State.LastRun?.Add(MaximumTimeout.Value) < Context.Scheduler.Now => EnergyConsumerState.CriticallyNeedsEnergy,
            false when HomeAssistant.CriticallyNeededSensor != null && HomeAssistant.CriticallyNeededSensor.IsOn() => EnergyConsumerState.CriticallyNeedsEnergy,
            false => EnergyConsumerState.NeedsEnergy,
        };
    }

    protected override bool CanStart()
    {
        State.State = GetState();

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
        HomeAssistant.SocketSwitch.TurnedOff -= Socket_TurnedOff;
        HomeAssistant.Dispose();

        MqttSensors.Dispose();

        ConsumptionMonitorTask?.Dispose();
    }
}