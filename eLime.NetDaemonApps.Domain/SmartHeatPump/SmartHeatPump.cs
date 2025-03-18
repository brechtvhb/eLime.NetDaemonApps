using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Domain.Mqtt;
using eLime.NetDaemonApps.Domain.Storage;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.HassModel;
using System.Reactive.Concurrency;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("eLime.NetDaemonApps.Tests")]
namespace eLime.NetDaemonApps.Domain.SmartHeatPump;

public class SmartHeatPump : IDisposable
{
    private const string smartGridReadyModeSelectName = "select.heat_pump_smart_grid_ready_mode";

    private readonly IHaContext _haContext;
    private readonly ILogger _logger;
    private readonly IScheduler _scheduler;
    private readonly IFileStorage _fileStorage;
    private readonly IMqttEntityManager _mqttEntityManager;

    private BinarySwitch SmartGridReadyInput1 { get; }
    private BinarySwitch SmartGridReadyInput2 { get; }

    public SmartGridReadyMode SmartGridReadyMode { get; private set; }
    private IDisposable? SmartGridReadyModeChangedEventHandlerObservable { get; set; }
    private readonly DebounceDispatcher? UpdateInHomeAssistantAndSaveDebounceDispatcher;

    public SmartHeatPump(SmartHeatPumpConfiguration configuration)
    {
        _haContext = configuration.HaContext;
        _logger = configuration.Logger;
        _scheduler = configuration.Scheduler;
        _fileStorage = configuration.FileStorage;
        _mqttEntityManager = configuration.MqttEntityManager;
        SmartGridReadyInput1 = configuration.SmartGridReadyInput1;
        SmartGridReadyInput2 = configuration.SmartGridReadyInput2;

        if (configuration.DebounceDuration != TimeSpan.Zero)
            UpdateInHomeAssistantAndSaveDebounceDispatcher = new DebounceDispatcher(configuration.DebounceDuration);

        GetState();
        EnsureEntitiesExist().RunSync();
        UpdateInHomeAssistant().RunSync();
    }


    private async Task EnsureEntitiesExist()
    {

        var smartGridReadySelectOptions = new SelectOptions
        {
            Icon = "phu:smarthome-solver",
            Options = Enum<SmartGridReadyMode>.AllValuesAsStringList(),
            Device = GetDevice()
        };

        await _mqttEntityManager.CreateAsync(smartGridReadyModeSelectName, new EntityCreationOptions(UniqueId: smartGridReadyModeSelectName, Name: "Smart grid ready mode", DeviceClass: "select", Persist: true), smartGridReadySelectOptions);
        var smartGridReadyModeObservable = await _mqttEntityManager.PrepareCommandSubscriptionAsync(smartGridReadyModeSelectName);
        SmartGridReadyModeChangedEventHandlerObservable = smartGridReadyModeObservable.SubscribeAsync(SmartGridReadyModeChangedEventHandler());
    }


    private Func<string, Task> SmartGridReadyModeChangedEventHandler()
    {
        return async state =>
        {
            _logger.LogDebug("Smart heat pump: Setting smart grid ready mode to {State}.", state);
            SetSmartGridReadyInputs(Enum<SmartGridReadyMode>.Cast(state));
            await DebounceUpdateInHomeAssistantAndSave();
        };
    }

    private void SetSmartGridReadyInputs(SmartGridReadyMode mode)
    {
        SmartGridReadyMode = mode;

        if (SmartGridReadyMode == SmartGridReadyMode.Blocked)
        {
            SmartGridReadyInput2.TurnOn();
            SmartGridReadyInput1.TurnOff();
        }
        else if (SmartGridReadyMode == SmartGridReadyMode.Normal)
        {
            SmartGridReadyInput2.TurnOff();
            SmartGridReadyInput1.TurnOff();
        }
        else if (SmartGridReadyMode == SmartGridReadyMode.Boosted)
        {
            SmartGridReadyInput2.TurnOff();
            SmartGridReadyInput1.TurnOn();
        }
        else if (SmartGridReadyMode == SmartGridReadyMode.Maximized)
        {
            SmartGridReadyInput2.TurnOn();
            SmartGridReadyInput1.TurnOn();
        }
    }

    private Device GetDevice()
    {
        return new Device { Identifiers = ["smart_heat_pump"], Name = "Smart heat pump", Manufacturer = "Me" };
    }

    private async Task DebounceUpdateInHomeAssistantAndSave()
    {
        if (UpdateInHomeAssistantAndSaveDebounceDispatcher == null)
        {
            Save();
            await UpdateInHomeAssistantAndSave();
            return;
        }

        Save();
        await UpdateInHomeAssistantAndSaveDebounceDispatcher.DebounceAsync(UpdateInHomeAssistantAndSave);
    }


    private void GetState()
    {
        var fileStorage = _fileStorage.Get<SmartHeatPumpFileStorage>("SmartHeatPump", "SmartHeatPump");

        if (fileStorage == null)
            return;

        SmartGridReadyMode = fileStorage.SmartGridReadyMode;

        _logger.LogDebug("Retrieved Smart heat pump state.");
    }

    private async Task UpdateInHomeAssistantAndSave()
    {
        Save();
        await UpdateInHomeAssistant();
    }

    private void Save()
    {
        _fileStorage.Save("SmartHeatPump", "SmartHeatPump", ToFileStorage());
    }

    private async Task UpdateInHomeAssistant()
    {
        //var globalAttributes = new EnergyManagerAttributes()
        //{
        //    LastUpdated = DateTime.Now.ToString("O"),
        //};

        await _mqttEntityManager.SetStateAsync(smartGridReadyModeSelectName, SmartGridReadyMode.ToString());
        //await _mqttEntityManager.SetAttributesAsync("sensor.energy_manager_state", globalAttributes);
    }

    private SmartHeatPumpFileStorage ToFileStorage() => new()
    {
        SmartGridReadyMode = SmartGridReadyMode
    };



    public void Dispose()
    {
        SmartGridReadyModeChangedEventHandlerObservable?.Dispose();
    }
}