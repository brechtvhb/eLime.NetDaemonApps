using eLime.NetDaemonApps.Domain.Helper;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

#pragma warning disable CS8618, CS9264

[assembly: InternalsVisibleTo("eLime.NetDaemonApps.Tests")]
namespace eLime.NetDaemonApps.Domain.EnergyManager2.BatteryManager;

internal class BatteryManager
{
    protected EnergyManagerContext Context { get; private set; }

    internal BatteryManagerState State { get; private set; }
    internal BatteryManagerHomeAssistantEntities HomeAssistant { get; private set; }

    internal List<Battery2> Batteries { get; private set; }

    internal DebounceDispatcher? SaveAndPublishStateDebounceDispatcher { get; private set; }

    private BatteryManager()
    {
    }

    public static async Task<BatteryManager> Create(EnergyManagerContext context, BatteryManagerConfiguration config)
    {
        var batteryManager = new BatteryManager();
        await batteryManager.Initialize(context, config);

        batteryManager.SaveAndPublishStateDebounceDispatcher = new DebounceDispatcher(TimeSpan.FromSeconds(1));

        //await battery.MqttSensors.CreateOrUpdateEntities(config.ConsumerGroups);
        batteryManager.GetAndSanitizeState();
        await batteryManager.SaveAndPublishState();

        return batteryManager;
    }

    private async Task Initialize(EnergyManagerContext context, BatteryManagerConfiguration config)
    {
        Context = context;
        HomeAssistant = new BatteryManagerHomeAssistantEntities(config);
        Batteries = new List<Battery2>();
        foreach (var x in config.Batteries)
        {
            var consumer = await Battery2.Create(Context, x);
            Batteries.Add(consumer);
        }
    }

    private void GetAndSanitizeState()
    {
        var persistedState = Context.FileStorage.Get<BatteryManagerState>("EnergyManager", "Batteries");
        State = persistedState ?? new BatteryManagerState();

        Context.Logger.LogDebug("Retrieved state of battery manager");
    }

    private Task SaveAndPublishState()
    {
        Context.FileStorage.Save("EnergyManager", "Batteries", State);
        //await MqttSensors.PublishState(State);
        return Task.CompletedTask;
    }

}