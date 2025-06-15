using eLime.NetDaemonApps.Domain.EnergyManager2.Configuration;
using eLime.NetDaemonApps.Domain.EnergyManager2.HomeAssistant;
using eLime.NetDaemonApps.Domain.EnergyManager2.PersistableState;
using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Domain.Storage;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using System.Reactive.Concurrency;
using System.Runtime.CompilerServices;

#pragma warning disable CS8618, CS9264

[assembly: InternalsVisibleTo("eLime.NetDaemonApps.Tests")]
namespace eLime.NetDaemonApps.Domain.EnergyManager2;

internal class BatteryManager
{
    protected ILogger Logger { get; private set; }
    protected IFileStorage FileStorage { get; private set; }
    protected IScheduler Scheduler { get; private set; }

    internal BatteryManagerState State { get; private set; }
    internal BatteryManagerHomeAssistantEntities HomeAssistant { get; private set; }

    internal List<Battery2> Batteries { get; private set; }

    internal DebounceDispatcher? SaveAndPublishStateDebounceDispatcher { get; private set; }

    private BatteryManager()
    {
    }

    public static async Task<BatteryManager> Create(ILogger logger, IFileStorage fileStorage, IScheduler scheduler, string timeZone, IMqttEntityManager mqtt, BatteryManagerConfiguration config)
    {
        var batteryManager = new BatteryManager();
        await batteryManager.Initialize(logger, fileStorage, scheduler, timeZone, mqtt, config);

        batteryManager.SaveAndPublishStateDebounceDispatcher = new DebounceDispatcher(TimeSpan.FromSeconds(1));

        //await battery.MqttSensors.CreateOrUpdateEntities(config.ConsumerGroups);
        batteryManager.GetAndSanitizeState();
        await batteryManager.SaveAndPublishState();

        return batteryManager;
    }

    private async Task Initialize(ILogger logger, IFileStorage fileStorage, IScheduler scheduler, string timeZone, IMqttEntityManager mqtt, BatteryManagerConfiguration config)
    {
        Logger = logger;
        FileStorage = fileStorage;
        Scheduler = scheduler;

        HomeAssistant = new BatteryManagerHomeAssistantEntities(config);
        Batteries = new List<Battery2>();
        foreach (var x in config.Batteries)
        {
            var consumer = await Battery2.Create(Logger, FileStorage, Scheduler, mqtt, timeZone, x);
            Batteries.Add(consumer);
        }
    }

    private void GetAndSanitizeState()
    {
        var persistedState = FileStorage.Get<BatteryManagerState>("EnergyManager", "Batteries");
        State = persistedState ?? new BatteryManagerState();

        Logger.LogDebug("Retrieved state of battery manager");
    }

    private Task SaveAndPublishState()
    {
        FileStorage.Save("EnergyManager", "Batteries", State);
        //await MqttSensors.PublishState(State);
        return Task.CompletedTask;
    }

}