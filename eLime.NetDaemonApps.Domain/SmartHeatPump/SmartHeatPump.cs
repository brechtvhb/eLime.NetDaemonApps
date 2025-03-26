using eLime.NetDaemonApps.Domain.Entities;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.Entities.TextSensors;
using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Domain.Storage;
using Microsoft.Extensions.Logging;
using NetDaemon.HassModel;
using System.Reactive.Concurrency;
using System.Runtime.CompilerServices;

#pragma warning disable CS8618, CS9264

[assembly: InternalsVisibleTo("eLime.NetDaemonApps.Tests")]
namespace eLime.NetDaemonApps.Domain.SmartHeatPump;

public class SmartHeatPump : IDisposable
{
    internal IHaContext HaContext { get; private set; }
    internal ILogger Logger { get; private set; }
    internal IScheduler Scheduler { get; private set; }
    internal IFileStorage FileStorage { get; private set; }

    internal SmartHeatPumpState State { get; private set; }
    internal SmartHeatPumpHomeAssistantEntities HomeAssistant { get; private set; }
    internal SmartHeatPumpEntities Entities { get; private set; }

    internal DebounceDispatcher? SaveAndPublishStateDebounceDispatcher { get; private set; }


    private SmartHeatPump()
    {

    }
    public static async Task<SmartHeatPump> Create(SmartHeatPumpConfiguration configuration)
    {
        var smartHeatPump = new SmartHeatPump();
        await smartHeatPump.Initialize(configuration);
        return smartHeatPump;
    }

    private async Task Initialize(SmartHeatPumpConfiguration configuration)
    {
        HaContext = configuration.HaContext;
        Logger = configuration.Logger;
        Scheduler = configuration.Scheduler;
        FileStorage = configuration.FileStorage;

        HomeAssistant = new SmartHeatPumpHomeAssistantEntities(configuration);
        HomeAssistant.SourcePumpRunningSensor.TurnedOn += SourcePumpRunningSensor_TurnedOn;
        HomeAssistant.SourcePumpRunningSensor.TurnedOff += SourcePumpRunningSensor_TurnedOff;
        HomeAssistant.SourceTemperatureSensor.Changed += SourceTemperatureSensor_Changed;
        HomeAssistant.StatusBytesSensor.StateChanged += StatusBytes_Changed;

        HomeAssistant.HeatConsumedTodayIntegerSensor.Changed += HeatSensor_changed;
        HomeAssistant.HeatConsumedTodayDecimalsSensor.Changed += HeatSensor_changed;
        HomeAssistant.HeatProducedTodayIntegerSensor.Changed += HeatSensor_changed;
        HomeAssistant.HeatProducedTodayDecimalsSensor.Changed += HeatSensor_changed;

        HomeAssistant.HotWaterConsumedTodayIntegerSensor.Changed += HotWaterSensor_changed;
        HomeAssistant.HotWaterConsumedTodayDecimalsSensor.Changed += HotWaterSensor_changed;
        HomeAssistant.HotWaterProducedTodayIntegerSensor.Changed += HotWaterSensor_changed;
        HomeAssistant.HotWaterProducedTodayDecimalsSensor.Changed += HotWaterSensor_changed;

        Entities = new SmartHeatPumpEntities(configuration.MqttEntityManager);
        Entities.SmartGridReadyModeChangedEvent += SmartGridReadyModeChangedEvent;
        if (configuration.DebounceDuration != TimeSpan.Zero)
            SaveAndPublishStateDebounceDispatcher = new DebounceDispatcher(configuration.DebounceDuration);

        await Entities.Publish();
        GetAndSanitizeState();
        await SaveAndPublishState();
    }


    private async void SmartGridReadyModeChangedEvent(object? sender, SmartGridReadyModeChangedEventArgs e)
    {
        try
        {
            Logger.LogInformation($"Setting smart grid ready mode to {e.SmartGridReadyMode}.");
            State.SmartGridReadyMode = e.SmartGridReadyMode;
            await SetSmartGridReadyInputs();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not handle Smart grid ready mode state change.");
        }
    }

    private async void HeatSensor_changed(object? sender, NumericSensorEventArgs e)
    {
        try
        {
            var cop = CalculateCoefficientOfPerformance(HomeAssistant.HeatConsumedTodayIntegerSensor.State, HomeAssistant.HeatConsumedTodayDecimalsSensor.State, HomeAssistant.HeatProducedTodayIntegerSensor.State, HomeAssistant.HeatProducedTodayDecimalsSensor.State);

            if (cop != null)
                State.HeatCoefficientOfPerformance = cop.Value;
            await DebounceSaveAndPublishState();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not calculate coefficient of performance for heat.");
        }
    }

    private async void HotWaterSensor_changed(object? sender, NumericSensorEventArgs e)
    {
        try
        {
            var cop = CalculateCoefficientOfPerformance(HomeAssistant.HotWaterConsumedTodayIntegerSensor.State, HomeAssistant.HotWaterConsumedTodayDecimalsSensor.State, HomeAssistant.HotWaterProducedTodayIntegerSensor.State, HomeAssistant.HotWaterProducedTodayDecimalsSensor.State);

            if (cop != null)
                State.HotWaterCoefficientOfPerformance = cop.Value;
            await DebounceSaveAndPublishState();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not calculate coefficient of performance for hot water.");
        }
    }

    private double? CalculateCoefficientOfPerformance(double? integerConsumedToday, double? decimalsConsumedToday, double? integerProducedToday, double? decimalsProducedToday)
    {
        double consumedToday = 0;
        double producedToday = 0;

        if (integerConsumedToday != null && decimalsConsumedToday != null)
            consumedToday = integerConsumedToday.Value + decimalsConsumedToday.Value / 1000;
        if (integerProducedToday != null && decimalsProducedToday != null)
            producedToday = integerProducedToday.Value + decimalsProducedToday.Value / 1000;

        if (consumedToday < 1 || producedToday < 1)
            return null;

        return Math.Round(producedToday / consumedToday, 2);
    }


    private async void StatusBytes_Changed(object? sender, TextSensorEventArgs e)
    {
        try
        {
            //Bug in ISG (turns SG ready inputs on while SG ready state remains in correct state), this code makes sure everything is in sync
            Logger.LogDebug("Smart heat pump: ISG was not available for a while but came back online, we can assume it rebooted.");
            if (e.Old?.State == Constants.UNAVAILABLE && e.New?.State != Constants.UNAVAILABLE)
                await SetSmartGridReadyInputs();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not set SG ready state.");
        }
    }

    private async void SourceTemperatureSensor_Changed(object? sender, NumericSensorEventArgs e)
    {
        try
        {
            if (State.SourcePumpStartedAt == null)
                return;

            if (State.SourcePumpStartedAt.Value.AddMinutes(25) > Scheduler.Now)
                return;

            if (e.New?.State != null)
            {
                State.SourceTemperature = e.New.State.Value;
                await DebounceSaveAndPublishState();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not handle change of source temperature.");
        }
    }

    private async void SourcePumpRunningSensor_TurnedOn(object? sender, BinarySensorEventArgs e)
    {
        try
        {
            State.SourcePumpStartedAt = Scheduler.Now;
            await DebounceSaveAndPublishState();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not handle source pump turned on event.");
        }
    }

    private async void SourcePumpRunningSensor_TurnedOff(object? sender, BinarySensorEventArgs e)
    {
        try
        {
            if (State.SourcePumpStartedAt?.AddMinutes(25) <= Scheduler.Now && HomeAssistant.SourceTemperatureSensor.State != null)
                State.SourceTemperature = HomeAssistant.SourceTemperatureSensor.State.Value;

            State.SourcePumpStartedAt = null;
            await DebounceSaveAndPublishState();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not handle source pump turned off event.");
        }
    }

    private async Task SetSmartGridReadyInputs()
    {
        switch (State.SmartGridReadyMode)
        {
            case SmartGridReadyMode.Blocked:
                HomeAssistant.SmartGridReadyInput2.TurnOn();
                HomeAssistant.SmartGridReadyInput1.TurnOff();
                break;
            case SmartGridReadyMode.Normal:
                HomeAssistant.SmartGridReadyInput2.TurnOff();
                HomeAssistant.SmartGridReadyInput1.TurnOff();
                break;
            case SmartGridReadyMode.Boosted:
                HomeAssistant.SmartGridReadyInput2.TurnOff();
                HomeAssistant.SmartGridReadyInput1.TurnOn();
                break;
            case SmartGridReadyMode.Maximized:
                HomeAssistant.SmartGridReadyInput2.TurnOn();
                HomeAssistant.SmartGridReadyInput1.TurnOn();
                break;
        }
        await DebounceSaveAndPublishState();
    }

    private async Task DebounceSaveAndPublishState()
    {
        if (SaveAndPublishStateDebounceDispatcher == null)
        {
            await SaveAndPublishState();
            return;
        }

        await SaveAndPublishStateDebounceDispatcher.DebounceAsync(SaveAndPublishState);
    }


    private void GetAndSanitizeState()
    {
        var persistedState = FileStorage.Get<SmartHeatPumpState>("SmartHeatPump", "SmartHeatPump");

        State = persistedState ?? new SmartHeatPumpState();

        if (HomeAssistant.SourcePumpRunningSensor.IsOn() && State.SourcePumpStartedAt == null)
            State.SourcePumpStartedAt = Scheduler.Now;

        if (HomeAssistant.SourcePumpRunningSensor.IsOff() && State.SourcePumpStartedAt != null)
            State.SourcePumpStartedAt = null;

        State.HeatCoefficientOfPerformance ??= CalculateCoefficientOfPerformance(HomeAssistant.HeatConsumedTodayIntegerSensor.State, HomeAssistant.HeatConsumedTodayDecimalsSensor.State, HomeAssistant.HeatProducedTodayIntegerSensor.State, HomeAssistant.HeatProducedTodayDecimalsSensor.State);
        State.HotWaterCoefficientOfPerformance ??= CalculateCoefficientOfPerformance(HomeAssistant.HotWaterConsumedTodayIntegerSensor.State, HomeAssistant.HotWaterConsumedTodayDecimalsSensor.State, HomeAssistant.HotWaterProducedTodayIntegerSensor.State, HomeAssistant.HotWaterProducedTodayDecimalsSensor.State);

        Logger.LogDebug("Retrieved Smart heat pump state.");
    }

    private async Task SaveAndPublishState()
    {
        FileStorage.Save("SmartHeatPump", "SmartHeatPump", State);
        await Entities.PublishState(State);
    }

    public void Dispose()
    {
        HomeAssistant.SourcePumpRunningSensor.TurnedOn -= SourcePumpRunningSensor_TurnedOn;
        HomeAssistant.SourcePumpRunningSensor.TurnedOff -= SourcePumpRunningSensor_TurnedOff;
        HomeAssistant.SourceTemperatureSensor.Changed -= SourceTemperatureSensor_Changed;
        HomeAssistant.StatusBytesSensor.StateChanged -= StatusBytes_Changed;

        HomeAssistant.HeatConsumedTodayIntegerSensor.Changed -= HeatSensor_changed;
        HomeAssistant.HeatConsumedTodayDecimalsSensor.Changed -= HeatSensor_changed;
        HomeAssistant.HeatProducedTodayIntegerSensor.Changed -= HeatSensor_changed;
        HomeAssistant.HeatProducedTodayDecimalsSensor.Changed -= HeatSensor_changed;
        HomeAssistant.HotWaterConsumedTodayIntegerSensor.Changed -= HotWaterSensor_changed;
        HomeAssistant.HotWaterConsumedTodayDecimalsSensor.Changed -= HotWaterSensor_changed;
        HomeAssistant.HotWaterProducedTodayIntegerSensor.Changed -= HotWaterSensor_changed;
        HomeAssistant.HotWaterProducedTodayDecimalsSensor.Changed -= HotWaterSensor_changed;

        HomeAssistant.Dispose();

        Entities.SmartGridReadyModeChangedEvent -= SmartGridReadyModeChangedEvent;
        Entities.Dispose();
    }
}