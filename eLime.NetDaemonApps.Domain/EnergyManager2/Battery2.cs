using eLime.NetDaemonApps.Domain.EnergyManager2.Configuration;
using eLime.NetDaemonApps.Domain.EnergyManager2.HomeAssistant;
using eLime.NetDaemonApps.Domain.EnergyManager2.PersistableState;
using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Domain.Storage;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.EnergyManager2;

public class Battery2 : IDisposable
{
    protected ILogger Logger { get; }
    protected IFileStorage FileStorage { get; }
    protected IScheduler Scheduler { get; }

    internal BatteryState State { get; private set; }
    internal BatteryHomeAssistantEntities HomeAssistant { get; }

    internal string Name { get; private set; }
    internal decimal Capacity { get; private set; }
    internal int MaxChargePower { get; private set; }
    internal int MaxDischargePower { get; private set; }
    internal bool CanCharge => HomeAssistant.MaxChargePowerNumber.State is > 0;
    internal bool CanDischarge => HomeAssistant.MaxDischargePowerNumber.State is > 0;
    internal double CurrentLoad => HomeAssistant.PowerSensor.State ?? 0;

    internal string Timezone { get; private set; }

    internal DebounceDispatcher? SaveAndPublishStateDebounceDispatcher { get; private set; }

    internal Battery2(ILogger logger, IFileStorage fileStorage, IScheduler scheduler, string timeZone, BatteryConfiguration config)
    {
        Logger = logger;
        FileStorage = fileStorage;
        Scheduler = scheduler;

        HomeAssistant = new BatteryHomeAssistantEntities(config);

        Timezone = timeZone;

        Name = config.Name;
        Capacity = config.Capacity;
        MaxChargePower = config.MaxChargePower;
        MaxDischargePower = config.MaxDischargePower;

    }
    public static async Task<Battery2> Create(ILogger logger, IFileStorage fileStorage, IScheduler scheduler, IMqttEntityManager mqttEntityManager, string timeZone, BatteryConfiguration config)
    {
        var battery = new Battery2(logger, fileStorage, scheduler, timeZone, config);

        battery.SaveAndPublishStateDebounceDispatcher = new DebounceDispatcher(TimeSpan.FromSeconds(1));

        //await battery.MqttSensors.CreateOrUpdateEntities(config.ConsumerGroups);
        battery.GetAndSanitizeState();
        await battery.SaveAndPublishState();

        return battery;
    }

    internal void GetAndSanitizeState()
    {
        var persistedState = FileStorage.Get<BatteryState>("EnergyManager", Name);
        State = persistedState ?? new BatteryState();

        Logger.LogDebug("{Name}: Retrieved state", Name);
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

    internal Task SaveAndPublishState()
    {
        FileStorage.Save("EnergyManager", Name, State);
        //await MqttSensors.PublishState(State);
        return Task.CompletedTask;
    }

    public async Task DisableCharging()
    {
        if (!CanCharge)
            return;

        HomeAssistant.MaxChargePowerNumber.Change(0);
        State.LastChange = Scheduler.Now;
        Logger.LogInformation("{Battery}: Battery will no longer charge.", Name);
        await DebounceSaveAndPublishState();
    }

    public async Task EnableCharging()
    {
        if (CanCharge)
            return;

        HomeAssistant.MaxChargePowerNumber.Change(MaxChargePower);
        State.LastChange = Scheduler.Now;
        Logger.LogInformation("{Battery}: Battery is allowed to charge at max {maxChargePower}W.", Name, MaxChargePower);
        await DebounceSaveAndPublishState();
    }

    public async Task DisableDischarging()
    {
        if (!CanDischarge)
            return;

        HomeAssistant.MaxDischargePowerNumber.Change(0);
        State.LastChange = Scheduler.Now;
        Logger.LogInformation("{Battery}: Battery will no longer discharge.", Name);
        await DebounceSaveAndPublishState();
    }

    public async Task EnableDischarging()
    {
        if (CanDischarge)
            return;

        HomeAssistant.MaxDischargePowerNumber.Change(MaxDischargePower);
        State.LastChange = Scheduler.Now;
        Logger.LogInformation("{Battery}: Battery is allowed to discharge at max {maxDisChargePower}W.", Name, MaxDischargePower);
        await DebounceSaveAndPublishState();
    }

    public void Dispose()
    {
        // TODO release managed resources here
    }
}