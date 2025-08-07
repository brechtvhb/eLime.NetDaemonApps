using eLime.NetDaemonApps.Domain.EnergyManager.BatteryManager.Batteries;
using eLime.NetDaemonApps.Domain.Helper;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

#pragma warning disable CS8618, CS9264

[assembly: InternalsVisibleTo("eLime.NetDaemonApps.Tests")]
namespace eLime.NetDaemonApps.Domain.EnergyManager.BatteryManager;

internal class BatteryManager : IDisposable
{
    protected EnergyManagerContext Context { get; private set; }

    internal BatteryManagerState State { get; private set; }
    internal BatteryManagerHomeAssistantEntities HomeAssistant { get; private set; }
    internal BatteryManagerMqttSensors MqttSensors { get; private set; }
    internal List<Battery> Batteries { get; private set; }

    internal DebounceDispatcher? SaveAndPublishStateDebounceDispatcher { get; private set; }

    internal double MaximumDischargePower => Batteries.Where(x => x is { IsEmpty: false }).Sum(x => x.MaxDischargePower);
    private List<double> _dischargeThresholds => [95, 85, 55, 25, 15];
    private BatteryManager()
    {
    }

    public static async Task<BatteryManager> Create(EnergyManagerContext context, BatteryManagerConfiguration config)
    {
        var batteryManager = new BatteryManager();
        await batteryManager.Initialize(context, config);

        batteryManager.SaveAndPublishStateDebounceDispatcher = new DebounceDispatcher(context.DebounceDuration);

        await batteryManager.MqttSensors.CreateOrUpdateEntities();
        batteryManager.GetAndSanitizeState();
        await batteryManager.SaveAndPublishState();

        return batteryManager;
    }

    private async Task Initialize(EnergyManagerContext context, BatteryManagerConfiguration config)
    {
        Context = context;
        MqttSensors = new BatteryManagerMqttSensors(context);
        HomeAssistant = new BatteryManagerHomeAssistantEntities(config);
        Batteries = [];
        foreach (var x in config.Batteries)
        {
            var battery = await Battery.Create(Context, x);
            battery.StateOfChargeChanged += Battery_StateOfChargeChanged;
            Batteries.Add(battery);
        }
    }
    internal List<Battery> BatteryPickOrderList
    {
        get
        {
            var offset = (Context.Scheduler.Now.DayOfYear - 1) % Batteries.Count;
            return Batteries.Skip(offset).Concat(Batteries.Take(offset)).ToList();
        }
    }

    internal List<Battery> BatteryDischargePickOrderList
    {
        get
        {
            if (Batteries.Any(x => x.HomeAssistant.StateOfChargeSensor.State == null))
                return BatteryPickOrderList;



            foreach (var threshold in _dischargeThresholds.Where(threshold => Batteries.Any(x => x.HomeAssistant.StateOfChargeSensor.State!.Value > threshold) && !Batteries.All(x => x.HomeAssistant.StateOfChargeSensor.State!.Value > threshold)))
            {
                return BatteryPickOrderList
                    .Where(x => x.HomeAssistant.StateOfChargeSensor.State!.Value > threshold)
                    .Concat(BatteryPickOrderList.Where(x => x.HomeAssistant.StateOfChargeSensor.State!.Value <= threshold))
                    .ToList();
            }

            return BatteryPickOrderList;
        }
    }


    internal async Task ManageBatteryPowerSettings(bool dynamicConsumersRunning, bool allowBatteryPowerConsumersRunning, double averageDischargePower)
    {
        if (Batteries.Count == 0)
            return;

        var canDischarge = !dynamicConsumersRunning || allowBatteryPowerConsumersRunning;
        if (canDischarge)
        {
            if (Batteries.All(x => !x.CanDischarge || x.IsEmpty))
                await ScaleUpDischarging(averageDischargePower);
            else if (Batteries.Any(x => x.AboveOptimalDischargePowerMaxThreshold))
                await ScaleUpDischarging(averageDischargePower);
            else if (Batteries.Count(x => x is { CanDischarge: true, IsEmpty: false }) > 1 && Batteries.Any(x => x.BelowOptimalDischargePowerMinThreshold))
                await ScaleDownDischarging(averageDischargePower);
        }
        else
        {
            foreach (var battery in Batteries.Where(battery => battery is { CanDischarge: true, CanControl: true }))
                await battery.DisableDischarging(reason: "Not allowed to discharge due to other consumers running");
        }
    }

    private async Task RotateBatteries()
    {
        var batteriesCurrentlyDischarging = Batteries.Where(x => x.CanDischarge).ToList();
        var batteriesThatShouldBeDischarging = BatteryDischargePickOrderList.Take(batteriesCurrentlyDischarging.Count).ToList();

        foreach (var battery in batteriesCurrentlyDischarging.Where(battery => !batteriesThatShouldBeDischarging.Select(x => x.Name).Contains(battery.Name)))
            await battery.DisableDischarging(reason: "Battery pick order changed");

        foreach (var battery in batteriesThatShouldBeDischarging.Where(battery => !batteriesCurrentlyDischarging.Select(x => x.Name).Contains(battery.Name)))
            await battery.EnableDischarging(reason: "Battery pick order changed");
    }

    private async Task ScaleUpDischarging(double averageDischargePower)
    {
        var optimalChargePowerMaxThreshold = 0;
        var index = 0;
        var availableBatteries = Batteries.Count(x => !x.IsEmpty);
        while (optimalChargePowerMaxThreshold <= averageDischargePower && index < availableBatteries)
        {
            var battery = BatteryDischargePickOrderList.Where(x => !x.IsEmpty).Skip(index).First();
            if (battery is { CanDischarge: false, CanControl: true })
                await battery.EnableDischarging(reason: "Average discharge power too high or no other load running that disables battery power");

            if (!battery.IsEmpty)
                optimalChargePowerMaxThreshold += battery.OptimalChargePowerMaxThreshold;
            index++;
        }
    }

    private async Task ScaleDownDischarging(double averageDischargePower)
    {
        var optimalChargePowerMinThreshold = Batteries.Where(x => x is { CanDischarge: true, IsEmpty: false }).Sum(x => x.OptimalDischargePowerMinThreshold);
        var batteriesThatCanDischarge = Batteries.Count(x => x is { CanDischarge: true, IsEmpty: false });
        var index = batteriesThatCanDischarge - 1;

        if (batteriesThatCanDischarge == 1)
            return;

        while (optimalChargePowerMinThreshold >= averageDischargePower && index > 0) //index >= 0 would disable discharging on all batteries
        {
            var battery = BatteryDischargePickOrderList.Skip(index).First();
            if (battery is { CanDischarge: true, CanControl: true })
            {
                await battery.DisableDischarging(reason: "Average discharge power too low");
                optimalChargePowerMinThreshold -= battery.OptimalDischargePowerMinThreshold;
            }

            index--;
        }
    }

    private async void Battery_StateOfChargeChanged(object? sender, Entities.NumericSensors.NumericSensorEventArgs e)
    {
        try
        {
            State.RemainingAvailableCapacity = Batteries.Sum(x => x.RemainingAvailableCapacity);
            State.StateOfCharge = Convert.ToInt32(State.RemainingAvailableCapacity / State.TotalAvailableCapacity * 100);

            if (e.Sensor.State is not null && _dischargeThresholds.Any(x => x == e.Sensor.State.Value))
                await RotateBatteries();

            await DebounceSaveAndPublishState();
        }
        catch (Exception ex)
        {
            Context.Logger.LogError(ex, "Could not update aggregate state of charge for batteries.");
        }
    }

    private void GetAndSanitizeState()
    {
        var persistedState = Context.FileStorage.Get<BatteryManagerState>("EnergyManager", "_batteries");
        State = persistedState ?? new BatteryManagerState();

        State.TotalAvailableCapacity = Batteries.Sum(b => b.AvailableCapacity);
        Context.Logger.LogDebug("Retrieved state of battery manager");
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

    private async Task SaveAndPublishState()
    {
        Context.FileStorage.Save("EnergyManager", "_batteries", State);
        await MqttSensors.PublishState(State);
    }

    public void Dispose()
    {
        foreach (var battery in Batteries)
        {
            battery.StateOfChargeChanged -= Battery_StateOfChargeChanged;
            battery.Dispose();
        }

        HomeAssistant.Dispose();
        MqttSensors.Dispose();
    }
}