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
    private List<Battery> BatteryPickOrderList
    {
        get
        {
            var offset = (Context.Scheduler.Now.DayOfYear - 1) % Batteries.Count;
            return Batteries.Skip(offset).Concat(Batteries.Take(offset)).ToList();
        }
    }


    internal async Task ManageBatteryPowerSettings(bool dynamicConsumersRunning, bool allowBatteryPowerConsumersRunning)
    {
        //TODO: Add OptimalChargeThreshold & OptimalDischargeThreshold setting, use BatteryPickOrderList of amount of power available is limited so we can maximize round trip efficiency.
        var canDischarge = !dynamicConsumersRunning || allowBatteryPowerConsumersRunning;
        if (canDischarge)
        {
            foreach (var battery in Batteries.Where(battery => !battery.CanDischarge))
                await battery.EnableDischarging();
        }
        else
        {
            foreach (var battery in Batteries.Where(battery => battery.CanDischarge))
                await battery.DisableDischarging();
        }
    }
    private async void Battery_StateOfChargeChanged(object? sender, Entities.NumericSensors.NumericSensorEventArgs e)
    {
        try
        {
            State.RemainingAvailableCapacity = Batteries.Sum(x => x.RemainingAvailableCapacity);
            State.StateOfCharge = Convert.ToInt32(State.RemainingAvailableCapacity / State.TotalAvailableCapacity * 100);
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