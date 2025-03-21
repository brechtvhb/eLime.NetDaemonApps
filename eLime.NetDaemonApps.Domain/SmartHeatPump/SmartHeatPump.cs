using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Domain.Mqtt;
using eLime.NetDaemonApps.Domain.Storage;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.HassModel;
using System.Globalization;
using System.Reactive.Concurrency;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("eLime.NetDaemonApps.Tests")]
namespace eLime.NetDaemonApps.Domain.SmartHeatPump;

public class SmartHeatPump : IDisposable
{
    private const string smartGridReadyModeSelectName = "select.heat_pump_smart_grid_ready_mode";
    private const string sourceTemperatureSensorName = "sensor.heat_pump_source_temperature";

    private readonly IHaContext _haContext;
    private readonly ILogger _logger;
    private readonly IScheduler _scheduler;
    private readonly IFileStorage _fileStorage;
    private readonly IMqttEntityManager _mqttEntityManager;

    private SmartHeatPumpState State { get; set; }
    private BinarySwitch SmartGridReadyInput1 { get; }
    private BinarySwitch SmartGridReadyInput2 { get; }
    private IDisposable? SmartGridReadyModeChangedEventHandlerObservable { get; set; }
    private readonly DebounceDispatcher? UpdateInHomeAssistantAndSaveDebounceDispatcher;

    private BinarySensor SourcePumpRunningSensor { get; }
    private NumericSensor SourceTemperatureSensor { get; }

    public SmartHeatPump(SmartHeatPumpConfiguration configuration)
    {
        _haContext = configuration.HaContext;
        _logger = configuration.Logger;
        _scheduler = configuration.Scheduler;
        _fileStorage = configuration.FileStorage;
        _mqttEntityManager = configuration.MqttEntityManager;

        SmartGridReadyInput1 = configuration.SmartGridReadyInput1;
        SmartGridReadyInput1.TurnedOn += SmartGridReadyInput_TurnedOn;
        SmartGridReadyInput2 = configuration.SmartGridReadyInput2;
        SmartGridReadyInput2.TurnedOn += SmartGridReadyInput_TurnedOn;

        SourcePumpRunningSensor = configuration.SourcePumpRunningSensor;
        SourcePumpRunningSensor.TurnedOn += SourcePumpRunningSensor_TurnedOn;
        SourcePumpRunningSensor.TurnedOff += SourcePumpRunningSensor_TurnedOff;
        SourceTemperatureSensor = configuration.SourceTemperatureSensor;
        SourceTemperatureSensor.Changed += SourceTemperatureSensor_Changed;

        if (configuration.DebounceDuration != TimeSpan.Zero)
            UpdateInHomeAssistantAndSaveDebounceDispatcher = new DebounceDispatcher(configuration.DebounceDuration);

        GetState();
        EnsureEntitiesExist().RunSync();
        UpdateInHomeAssistant().RunSync();
    }
    private async void SmartGridReadyInput_TurnedOn(object? sender, BinarySensorEventArgs e)
    {
        try
        {
            //Bug in ISG (turns SG ready inputs on while SG ready state remains in correct state), this code makes sure everything is in sync
            _logger.LogDebug("Smart heat pump: Smart grid ready input went from available to on, resetting to correct state.");
            if (e.Old?.State == "unavailable" && e.New?.State == "on")
                await SetSmartGridReadyInputs();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not set SG ready state.");
        }
    }
    private async void SourceTemperatureSensor_Changed(object? sender, NumericSensorEventArgs e)
    {
        try
        {
            if (State.SourcePumpStartedAt == null)
                return;

            if (State.SourcePumpStartedAt.Value.AddMinutes(25) > _scheduler.Now)
                return;

            if (e.New?.State != null)
            {
                State.SourceTemperature = e.New.State.Value;
                await DebounceUpdateInHomeAssistantAndSave();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not handle change of source temperature.");
        }
    }

    private async void SourcePumpRunningSensor_TurnedOn(object? sender, BinarySensorEventArgs e)
    {
        try
        {
            State.SourcePumpStartedAt = _scheduler.Now;
            await DebounceUpdateInHomeAssistantAndSave();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not handle source pump turned on event.");
        }
    }

    private async void SourcePumpRunningSensor_TurnedOff(object? sender, BinarySensorEventArgs e)
    {
        try
        {
            State.SourcePumpStartedAt = null;
            await DebounceUpdateInHomeAssistantAndSave();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not handle source pump turned off event.");
        }
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

        var sourceTemperatureSensorOptions = new NumericSensorOptions
        {
            StateClass = "measurement",
            UnitOfMeasurement = "°C",
            Icon = "fapro:oil-temperature",
            Device = GetDevice()
        };
        await _mqttEntityManager.CreateAsync(sourceTemperatureSensorName, new EntityCreationOptions(UniqueId: sourceTemperatureSensorName, Name: "Source temperature", DeviceClass: "temperature", Persist: true), sourceTemperatureSensorOptions);

    }


    private Func<string, Task> SmartGridReadyModeChangedEventHandler()
    {
        return async state =>
        {
            _logger.LogDebug("Smart heat pump: Setting smart grid ready mode to {State}.", state);
            State.SmartGridReadyMode = Enum<SmartGridReadyMode>.Cast(state);
            await SetSmartGridReadyInputs();
        };
    }

    private async Task SetSmartGridReadyInputs()
    {
        switch (State.SmartGridReadyMode)
        {
            case SmartGridReadyMode.Blocked:
                SmartGridReadyInput2.TurnOn();
                SmartGridReadyInput1.TurnOff();
                break;
            case SmartGridReadyMode.Normal:
                SmartGridReadyInput2.TurnOff();
                SmartGridReadyInput1.TurnOff();
                break;
            case SmartGridReadyMode.Boosted:
                SmartGridReadyInput2.TurnOff();
                SmartGridReadyInput1.TurnOn();
                break;
            case SmartGridReadyMode.Maximized:
                SmartGridReadyInput2.TurnOn();
                SmartGridReadyInput1.TurnOn();
                break;
        }
        await DebounceUpdateInHomeAssistantAndSave();
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
        var persistedState = _fileStorage.Get<SmartHeatPumpState>("SmartHeatPump", "SmartHeatPump");

        if (persistedState == null)
            return;

        State = persistedState;

        _logger.LogDebug("Retrieved Smart heat pump state.");
    }

    private async Task UpdateInHomeAssistantAndSave()
    {
        Save();
        await UpdateInHomeAssistant();
    }

    private void Save()
    {
        _fileStorage.Save("SmartHeatPump", "SmartHeatPump", State);
    }

    private async Task UpdateInHomeAssistant()
    {
        //var globalAttributes = new EnergyManagerAttributes()
        //{
        //    LastUpdated = DateTime.Now.ToString("O"),
        //};

        await _mqttEntityManager.SetStateAsync(smartGridReadyModeSelectName, State.SmartGridReadyMode.ToString());
        await _mqttEntityManager.SetStateAsync(sourceTemperatureSensorName, State.SourceTemperature.ToString("N", new NumberFormatInfo { NumberDecimalSeparator = "." }));
        //await _mqttEntityManager.SetAttributesAsync("sensor.energy_manager_state", globalAttributes);
    }



    public void Dispose()
    {
        SmartGridReadyModeChangedEventHandlerObservable?.Dispose();
    }
}