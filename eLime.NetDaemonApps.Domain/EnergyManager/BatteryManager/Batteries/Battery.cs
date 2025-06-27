using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.Helper;
using Microsoft.Extensions.Logging;

#pragma warning disable CS8618, CS9264

namespace eLime.NetDaemonApps.Domain.EnergyManager.BatteryManager.Batteries;

public class Battery : IDisposable
{
    protected EnergyManagerContext Context { get; }
    internal BatteryState State { get; private set; }
    internal BatteryHomeAssistantEntities HomeAssistant { get; }
    internal BatteryMqttSensors MqttSensors { get; }
    internal string Name { get; }
    internal decimal Capacity { get; }
    internal int MinimumStateOfCharge { get; }
    internal int RteStateOfChargeReferencePoint { get; }
    internal int MaxChargePower { get; }
    internal int OptimalChargePowerMinThreshold { get; }
    internal int OptimalChargePowerMaxThreshold { get; }
    internal int MaxDischargePower { get; }
    internal int OptimalDischargePowerMinThreshold { get; }
    internal int OptimalDischargePowerMaxThreshold { get; }
    internal bool CanCharge => HomeAssistant.MaxChargePowerNumber.State is > 0;
    internal bool IsEmpty => HomeAssistant.StateOfChargeSensor.State <= MinimumStateOfCharge;
    internal bool IsFull => HomeAssistant.StateOfChargeSensor.State >= 100;
    internal bool CanDischarge => HomeAssistant.MaxDischargePowerNumber.State is > 1; //Hopefully a temporary thing, Marstek controller takes power from grid when you set max discharge power to 0, it doesn't when you set it to 1
    internal double CurrentLoad => HomeAssistant.PowerSensor.State ?? 0;
    internal decimal MinimumCapacity => Math.Round(Capacity * MinimumStateOfCharge / 100m, 2);
    internal decimal AvailableCapacity => Capacity - MinimumCapacity;
    internal decimal RemainingCapacity => Math.Round(Capacity * Convert.ToDecimal(HomeAssistant.StateOfChargeSensor.State) / 100, 2);
    internal decimal RemainingAvailableCapacity => RemainingCapacity - MinimumCapacity;

    internal bool CanControl => State.LastChange == null || State.LastChange.Value.AddSeconds(30) < Context.Scheduler.Now;
    //Might need to average in time here
    internal bool AboveOptimalDischargePowerMaxThreshold => CanDischarge && -CurrentLoad > OptimalDischargePowerMaxThreshold;
    //Might need to average in time here
    internal bool BelowOptimalDischargePowerMinThreshold => CanDischarge && !IsEmpty && -CurrentLoad < OptimalDischargePowerMinThreshold;

    internal DebounceDispatcher? SaveAndPublishStateDebounceDispatcher { get; private set; }

    internal Battery(EnergyManagerContext context, BatteryConfiguration config)
    {
        Context = context;
        MqttSensors = new BatteryMqttSensors(config.Name, context);
        HomeAssistant = new BatteryHomeAssistantEntities(config);
        HomeAssistant.StateOfChargeSensor.Changed += StateOfChargeSensor_Changed;
        Name = config.Name;
        Capacity = config.Capacity;
        MinimumStateOfCharge = config.MinimumStateOfCharge;
        RteStateOfChargeReferencePoint = config.RteStateOfChargeReferencePoint;
        MaxChargePower = config.MaxChargePower;
        OptimalChargePowerMinThreshold = config.OptimalChargePowerMinThreshold;
        OptimalChargePowerMaxThreshold = config.OptimalChargePowerMaxThreshold;
        MaxDischargePower = config.MaxDischargePower;
        OptimalDischargePowerMinThreshold = config.OptimalDischargePowerMinThreshold;
        OptimalDischargePowerMaxThreshold = config.OptimalDischargePowerMaxThreshold;
    }

    public event EventHandler<NumericSensorEventArgs>? StateOfChargeChanged;
    protected void OnStateOfChargeChanged(NumericSensorEventArgs e)
    {
        StateOfChargeChanged?.Invoke(this, e);
    }

    private async void StateOfChargeSensor_Changed(object? sender, NumericSensorEventArgs e)
    {
        try
        {
            OnStateOfChargeChanged(e);

            if (Convert.ToInt32(e.Sensor.State) != RteStateOfChargeReferencePoint)
            {
                State.LastRteStateOfChargeReferencePoint = RteStateOfChargeReferencePoint;
                State.LastChange = Context.Scheduler.Now;
                await DebounceSaveAndPublishState();
                return;
            }

            Context.Logger.LogTrace("{Name}: State of charge is {RteStateOfChargeReferencePoint}%. Calculating Round trip efficiency.", Name, RteStateOfChargeReferencePoint);
            var totalEnergyCharged = HomeAssistant.TotalEnergyChargedSensor.State;
            var totalEnergyDischarged = HomeAssistant.TotalEnergyDischargedSensor.State;

            if (totalEnergyCharged is null || totalEnergyDischarged is null)
            {
                Context.Logger.LogTrace("{Name}: Total energy charged or discharged is null, cannot calculate RTE.", Name);
                return;
            }

            var energyChargedSinceLastRteCalculation = totalEnergyCharged.Value - State.LastTotalEnergyChargedAtRteReferencePoint;
            var energyDischargedSinceLastRteCalculation = totalEnergyDischarged.Value - State.LastTotalEnergyDischargedAtRteReferencePoint;

            if (energyChargedSinceLastRteCalculation <= 1 || energyDischargedSinceLastRteCalculation <= 1)
            {
                Context.Logger.LogTrace("{Name}: Not enough energy charged or discharged since last RTE calculation.", Name);
                return;
            }

            if (RteStateOfChargeReferencePoint == State.LastRteStateOfChargeReferencePoint)
            {
                var rte = Math.Round(energyDischargedSinceLastRteCalculation / energyChargedSinceLastRteCalculation * 100, 2);
                State.RoundTripEfficiency = rte;
                Context.Logger.LogInformation("{Name}: Round trip efficiency calculated: {Rte}%.", Name, rte);
            }

            State.LastTotalEnergyChargedAtRteReferencePoint = totalEnergyCharged.Value;
            State.LastTotalEnergyDischargedAtRteReferencePoint = totalEnergyDischarged.Value;
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
        State.LastChange = Context.Scheduler.Now;
        Context.Logger.LogInformation("{Battery}: Battery will no longer charge.", Name);
        await DebounceSaveAndPublishState();
    }

    public async Task EnableCharging()
    {
        if (CanCharge)
            return;

        HomeAssistant.MaxChargePowerNumber.Change(MaxChargePower);
        State.LastChange = Context.Scheduler.Now;
        Context.Logger.LogInformation("{Battery}: Battery is allowed to charge at max {maxChargePower}W.", Name, MaxChargePower);
        await DebounceSaveAndPublishState();
    }

    public async Task DisableDischarging()
    {
        if (!CanDischarge)
            return;

        HomeAssistant.MaxDischargePowerNumber.Change(1);
        State.LastChange = Context.Scheduler.Now;
        Context.Logger.LogInformation("{Battery}: Battery will no longer discharge.", Name);
        await DebounceSaveAndPublishState();
    }

    public async Task EnableDischarging()
    {
        if (CanDischarge)
            return;

        HomeAssistant.MaxDischargePowerNumber.Change(MaxDischargePower);
        State.LastChange = Context.Scheduler.Now;
        Context.Logger.LogInformation("{Battery}: Battery is allowed to discharge at max {maxDisChargePower}W.", Name, MaxDischargePower);
        await DebounceSaveAndPublishState();
    }

    public void Dispose()
    {
        MqttSensors.Dispose();
        HomeAssistant.StateOfChargeSensor.Changed -= StateOfChargeSensor_Changed;
        HomeAssistant.Dispose();
    }
}