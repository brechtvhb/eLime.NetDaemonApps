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
    private static List<double> socThresholds => [95, 85, 55, 25, 15];
    private BatteryManager()
    {
    }

    public static async Task<BatteryManager> Create(EnergyManagerContext context, BatteryManagerConfiguration config)
    {
        var batteryManager = new BatteryManager();
        await batteryManager.Initialize(context, config);

        if (context.DebounceDuration != TimeSpan.Zero)
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

            foreach (var threshold in socThresholds.Where(threshold => Batteries.Any(x => x.HomeAssistant.StateOfChargeSensor.State!.Value > threshold) && !Batteries.All(x => x.HomeAssistant.StateOfChargeSensor.State!.Value > threshold)))
            {
                return BatteryPickOrderList
                    .Where(x => x.HomeAssistant.StateOfChargeSensor.State!.Value > threshold)
                    .Concat(BatteryPickOrderList.Where(x => x.HomeAssistant.StateOfChargeSensor.State!.Value <= threshold))
                    .ToList();
            }

            return BatteryPickOrderList;
        }
    }


    internal List<Battery> BatteryChargePickOrderList
    {
        get
        {
            if (Batteries.Any(x => x.HomeAssistant.StateOfChargeSensor.State == null))
                return BatteryPickOrderList;

            foreach (var threshold in socThresholds.OrderBy(x => x).Where(threshold => Batteries.Any(x => x.HomeAssistant.StateOfChargeSensor.State!.Value < threshold) && !Batteries.All(x => x.HomeAssistant.StateOfChargeSensor.State!.Value < threshold)))
            {
                return BatteryPickOrderList
                    .Where(x => x.HomeAssistant.StateOfChargeSensor.State!.Value < threshold)
                    .Concat(BatteryPickOrderList.Where(x => x.HomeAssistant.StateOfChargeSensor.State!.Value >= threshold))
                    .ToList();
            }

            return BatteryPickOrderList;
        }
    }


    internal async Task ManageBatteryPowerSettings(bool dynamicConsumersRunning, bool allowBatteryPowerConsumersRunning, double averageDischargePower, double averageChargePower)
    {
        if (Batteries.Count == 0)
            return;

        await ManageDischargeSettings(dynamicConsumersRunning, allowBatteryPowerConsumersRunning, averageDischargePower);
        await ManageChargeSettings(averageChargePower);
    }

    private async Task ManageDischargeSettings(bool dynamicConsumersRunning, bool allowBatteryPowerConsumersRunning, double averageDischargePower)
    {
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

    private async Task ScaleUpDischarging(double averageDischargePower)
    {
        var optimalDischargePowerMaxThreshold = 0;
        var index = 0;
        var availableBatteries = Batteries.Count(x => !x.IsEmpty);
        while (optimalDischargePowerMaxThreshold <= averageDischargePower && index < availableBatteries)
        {
            var battery = BatteryDischargePickOrderList.Where(x => !x.IsEmpty).Skip(index).First();
            if (battery is { CanDischarge: false, CanControl: true })
                await battery.EnableDischarging(reason: "Average discharge power too high or no other load running that disables battery power");

            if (!battery.IsEmpty)
                optimalDischargePowerMaxThreshold += battery.OptimalDischargePowerMaxThreshold;
            index++;
        }
    }

    private async Task ScaleDownDischarging(double averageDischargePower)
    {
        var optimalDischargePowerMinThreshold = Batteries.Where(x => x is { CanDischarge: true, IsEmpty: false }).Sum(x => x.OptimalDischargePowerMinThreshold);
        var batteriesThatCanDischarge = Batteries.Count(x => x is { CanDischarge: true, IsEmpty: false });
        var index = batteriesThatCanDischarge - 1;

        if (batteriesThatCanDischarge == 1)
            return;

        while (optimalDischargePowerMinThreshold >= averageDischargePower && index > 0) //index >= 0 would disable discharging on all batteries
        {
            var battery = BatteryDischargePickOrderList.Skip(index).First();
            if (battery is { CanDischarge: true, CanControl: true })
            {
                await battery.DisableDischarging(reason: "Average discharge power too low");
                optimalDischargePowerMinThreshold -= battery.OptimalDischargePowerMinThreshold;
            }

            index--;
        }
    }
    private async Task ManageChargeSettings(double averageChargePower)
    {
        if (Batteries.All(x => !x.CanCharge || x.IsFull))
            await ScaleUpCharging(averageChargePower);
        else if (Batteries.Any(x => x.AboveOptimalChargePowerMaxThreshold))
            await ScaleUpCharging(averageChargePower);
        else if (Batteries.Count(x => x is { CanCharge: true, IsFull: false }) > 1 && Batteries.Any(x => x.BelowOptimalChargePowerMinThreshold))
            await ScaleDownCharging(averageChargePower);
    }


    private async Task ScaleUpCharging(double averageChargePower)
    {
        var optimalChargePowerMaxThreshold = 0;
        var index = 0;
        var availableBatteries = Batteries.Count(x => !x.IsFull);
        while (optimalChargePowerMaxThreshold <= averageChargePower && index < availableBatteries)
        {
            var battery = BatteryChargePickOrderList.Where(x => !x.IsFull).Skip(index).First();
            if (battery is { CanCharge: false, CanControl: true })
                await battery.EnableCharging(reason: "Average charge power is high enough for charging one more battery in parallel.");

            if (!battery.IsFull)
                optimalChargePowerMaxThreshold += battery.OptimalChargePowerMaxThreshold;
            index++;
        }
    }

    private async Task ScaleDownCharging(double averageChargePower)
    {
        var optimalChargePowerMinThreshold = Batteries.Where(x => x is { CanCharge: true, IsFull: false }).Sum(x => x.OptimalChargePowerMinThreshold);
        var batteriesThatCanCharge = Batteries.Count(x => x is { CanCharge: true, IsFull: false });
        var index = batteriesThatCanCharge - 1;

        if (batteriesThatCanCharge == 1)
            return;

        while (optimalChargePowerMinThreshold >= averageChargePower && index > 0) //index >= 0 would disable discharging on all batteries
        {
            var battery = BatteryChargePickOrderList.Skip(index).First();
            if (battery is { CanCharge: true, CanControl: true })
            {
                await battery.DisableCharging(reason: "Average charge power too low");
                optimalChargePowerMinThreshold -= battery.OptimalChargePowerMinThreshold;
            }

            index--;
        }
    }

    private async Task RotateBatteries()
    {
        await RotateDischarging();
        await RotateCharging();
    }

    private async Task RotateDischarging()
    {
        var batteriesCurrentlyDischarging = Batteries.Where(x => x.CanDischarge).ToList();
        var batteriesThatShouldBeDischarging = BatteryDischargePickOrderList.Take(batteriesCurrentlyDischarging.Count).ToList();

        foreach (var battery in batteriesCurrentlyDischarging.Where(battery => !batteriesThatShouldBeDischarging.Select(x => x.Name).Contains(battery.Name)))
            await battery.DisableDischarging(reason: "Battery pick order changed");

        foreach (var battery in batteriesThatShouldBeDischarging.Where(battery => !batteriesCurrentlyDischarging.Select(x => x.Name).Contains(battery.Name)))
            await battery.EnableDischarging(reason: "Battery pick order changed");
    }

    public async Task RotateCharging()
    {
        var batteriesCurrentlyCharging = Batteries.Where(x => x.CanCharge).ToList();
        var batteriesThatShouldBeCharging = BatteryChargePickOrderList.Take(batteriesCurrentlyCharging.Count).ToList();

        foreach (var battery in batteriesCurrentlyCharging.Where(battery => !batteriesThatShouldBeCharging.Select(x => x.Name).Contains(battery.Name)))
            await battery.DisableCharging(reason: "Battery pick order changed");

        foreach (var battery in batteriesThatShouldBeCharging.Where(battery => !batteriesCurrentlyCharging.Select(x => x.Name).Contains(battery.Name)))
            await battery.EnableCharging(reason: "Battery pick order changed");
    }

    private async void Battery_StateOfChargeChanged(object? sender, Entities.NumericSensors.NumericSensorEventArgs e)
    {
        try
        {
            State.RemainingAvailableCapacity = Batteries.Sum(x => x.RemainingAvailableCapacity);
            State.StateOfCharge = Convert.ToInt32(State.RemainingAvailableCapacity / State.TotalAvailableCapacity * 100);

            if (e.Sensor.State is not null && socThresholds.Any(x => x == e.Sensor.State.Value))
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