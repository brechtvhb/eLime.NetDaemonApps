using eLime.NetDaemonApps.Domain.Helper;
using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;

#pragma warning disable CS8618, CS9264

namespace eLime.NetDaemonApps.Domain.EnergyManager.BatteryManager;

public class Battery : IDisposable
{
    protected EnergyManagerContext Context { get; }
    internal BatteryState State { get; private set; }
    internal BatteryHomeAssistantEntities HomeAssistant { get; }
    internal BatteryMqttSensors MqttSensors { get; }
    internal string Name { get; }
    internal decimal Capacity { get; }
    internal int MaxChargePower { get; }
    internal int MaxDischargePower { get; }
    internal bool CanCharge => HomeAssistant.MaxChargePowerNumber.State is > 0;
    internal bool CanDischarge => HomeAssistant.MaxDischargePowerNumber.State is > 0;
    internal double CurrentLoad => HomeAssistant.PowerSensor.State ?? 0;

    internal DebounceDispatcher? SaveAndPublishStateDebounceDispatcher { get; private set; }

    internal Battery(EnergyManagerContext context, BatteryConfiguration config)
    {
        Context = context;
        MqttSensors = new BatteryMqttSensors(config.Name, context);
        HomeAssistant = new BatteryHomeAssistantEntities(config);
        HomeAssistant.StateOfChargeSensor.Changed += StateOfChargeSensor_Changed;
        Name = config.Name;
        Capacity = config.Capacity;
        MaxChargePower = config.MaxChargePower;
        MaxDischargePower = config.MaxDischargePower;

    }

    private async void StateOfChargeSensor_Changed(object? sender, Entities.NumericSensors.NumericSensorEventArgs e)
    {
        try
        {
            if (Convert.ToInt32(e.Sensor.State) != 50)
                return;

            Context.Logger.LogTrace("{Name}: State of charge is 50%. Calculating Round trip efficiency.", Name);
            var totalEnergyCharged = HomeAssistant.TotalEnergyChargedSensor.State;
            var totalEnergyDischarged = HomeAssistant.TotalEnergyDischargedSensor.State;

            if (totalEnergyCharged is null || totalEnergyDischarged is null)
            {
                Context.Logger.LogTrace("{Name}: Total energy charged or discharged is null, cannot calculate RTE.", Name);
                return;
            }

            var energyChargedSinceLastRteCalculation = totalEnergyCharged.Value - State.LastTotalEnergyChargedAt50Percent;
            var energyDischargedSinceLastRteCalculation = totalEnergyDischarged.Value - State.LastTotalEnergyDischargedAt50Percent;

            if (energyChargedSinceLastRteCalculation <= 1 || energyDischargedSinceLastRteCalculation <= 1)
            {
                Context.Logger.LogTrace("{Name}: Not enough energy charged or discharged since last RTE calculation.", Name);
                return;
            }
            var rte = Math.Round(energyDischargedSinceLastRteCalculation / energyChargedSinceLastRteCalculation * 100, 2);
            State.RoundTripEfficiency = rte;
            State.LastChange = Context.Scheduler.Now;
            State.LastTotalEnergyChargedAt50Percent = totalEnergyCharged.Value;
            State.LastTotalEnergyDischargedAt50Percent = totalEnergyDischarged.Value;
            Context.Logger.LogInformation("{Name}: Round trip efficiency calculated: {Rte}%.", Name, rte);
            await DebounceSaveAndPublishState();
        }
        catch (Exception ex)
        {
            Context.Logger.LogError(ex, "{Name}: Could not calculate RTE.", Name);
        }
    }

    public static async Task<Battery> Create(EnergyManagerContext context, BatteryConfiguration config)
    {
        var battery = new Battery(context, config);

        if (context.DebounceDuration != TimeSpan.Zero)
            battery.SaveAndPublishStateDebounceDispatcher = new DebounceDispatcher(context.DebounceDuration);

        await battery.MqttSensors.CreateOrUpdateEntities();
        battery.GetAndSanitizeState();
        await battery.SaveAndPublishState();

        return battery;
    }

    internal void GetAndSanitizeState()
    {
        var persistedState = Context.FileStorage.Get<BatteryState>("EnergyManager", Name.MakeHaFriendly());
        State = persistedState ?? new BatteryState();

        Context.Logger.LogDebug("{Name}: Retrieved state", Name);
    }

    protected async Task DebounceSaveAndPublishState()
    {
        if (SaveAndPublishStateDebounceDispatcher == null)
        {
            await SaveAndPublishState();
            return;
        }

        await SaveAndPublishStateDebounceDispatcher.DebounceAsync(SaveAndPublishState);
    }

    internal async Task SaveAndPublishState()
    {
        Context.FileStorage.Save("EnergyManager", Name.MakeHaFriendly(), State);
        await MqttSensors.PublishState(State);
    }

    public async Task DisableCharging()
    {
        if (!CanCharge)
            return;

        HomeAssistant.MaxChargePowerNumber.Change(0);
        State.LastChange = Scheduler.Now;
        Context.Logger.LogInformation("{Battery}: Battery will no longer charge.", Name);
        await DebounceSaveAndPublishState();
    }

    public async Task EnableCharging()
    {
        if (CanCharge)
            return;

        HomeAssistant.MaxChargePowerNumber.Change(MaxChargePower);
        State.LastChange = Scheduler.Now;
        Context.Logger.LogInformation("{Battery}: Battery is allowed to charge at max {maxChargePower}W.", Name, MaxChargePower);
        await DebounceSaveAndPublishState();
    }

    public async Task DisableDischarging()
    {
        if (!CanDischarge)
            return;

        HomeAssistant.MaxDischargePowerNumber.Change(0);
        State.LastChange = Scheduler.Now;
        Context.Logger.LogInformation("{Battery}: Battery will no longer discharge.", Name);
        await DebounceSaveAndPublishState();
    }

    public async Task EnableDischarging()
    {
        if (CanDischarge)
            return;

        HomeAssistant.MaxDischargePowerNumber.Change(MaxDischargePower);
        State.LastChange = Scheduler.Now;
        Context.Logger.LogInformation("{Battery}: Battery is allowed to discharge at max {maxDisChargePower}W.", Name, MaxDischargePower);
        await DebounceSaveAndPublishState();
    }

    public void Dispose()
    {
        HomeAssistant.StateOfChargeSensor.Changed -= StateOfChargeSensor_Changed;
        HomeAssistant.Dispose();
    }
}