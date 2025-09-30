using eLime.NetDaemonApps.Domain.Entities;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.Entities.TextSensors;
using eLime.NetDaemonApps.Domain.Helper;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.Scheduler;
using System.Reactive.Concurrency;
using System.Runtime.CompilerServices;

#pragma warning disable CS8618, CS9264


[assembly: InternalsVisibleTo("eLime.NetDaemonApps.Tests")]
namespace eLime.NetDaemonApps.Domain.SmartHeatPump;

public class SmartHeatPump : IDisposable
{
    internal SmartHeatPumpContext Context { get; private set; }
    internal TemperatureSettings TemperatureSettings { get; private set; }
    internal SmartHeatPumpState State { get; private set; }
    internal SmartHeatPumpHomeAssistantEntities HomeAssistant { get; private set; }
    internal SmartHeatPumpMqttSensors MqttSensors { get; private set; }

    protected IDisposable? MonitoringTask { get; private set; }

    internal DebounceDispatcher? SaveAndPublishStateDebounceDispatcher { get; private set; }
    internal DebounceDispatcher CalculateHeatCopDebounceDispatcher { get; private set; }
    internal DebounceDispatcher CalculateHotWaterCopDebounceDispatcher { get; private set; }

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
        Context = configuration.Context;
        TemperatureSettings = new TemperatureSettings(configuration.TemperatureConfiguration);

        HomeAssistant = new SmartHeatPumpHomeAssistantEntities(configuration);
        HomeAssistant.SourcePumpRunningSensor.TurnedOn += SourcePumpRunningSensor_TurnedOn;
        HomeAssistant.SourcePumpRunningSensor.TurnedOff += SourcePumpRunningSensor_TurnedOff;
        HomeAssistant.SourceTemperatureSensor.Changed += SourceTemperatureSensor_Changed;
        HomeAssistant.StatusBytesSensor.StateChanged += StatusBytes_Changed;

        HomeAssistant.RoomTemperatureSensor.Changed += RoomTemperatureSensor_Changed;
        HomeAssistant.HeatConsumedTodayIntegerSensor.Changed += HeatSensor_changed;
        HomeAssistant.HeatConsumedTodayDecimalsSensor.Changed += HeatSensor_changed;
        HomeAssistant.HeatProducedTodayIntegerSensor.Changed += HeatSensor_changed;
        HomeAssistant.HeatProducedTodayDecimalsSensor.Changed += HeatSensor_changed;

        HomeAssistant.HotWaterTemperatureSensor.Changed += HotWaterTemperatureSensor_Changed;
        HomeAssistant.HotWaterConsumedTodayIntegerSensor.Changed += HotWaterSensor_changed;
        HomeAssistant.HotWaterConsumedTodayDecimalsSensor.Changed += HotWaterSensor_changed;
        HomeAssistant.HotWaterProducedTodayIntegerSensor.Changed += HotWaterSensor_changed;
        HomeAssistant.HotWaterProducedTodayDecimalsSensor.Changed += HotWaterSensor_changed;

        MqttSensors = new SmartHeatPumpMqttSensors(Context);
        MqttSensors.SmartGridReadyModeChanged += SmartGridReadyModeChangedEvent;
        MqttSensors.ShowerRequested += OnShowerRequested;
        MqttSensors.BathRequested += OnBathRequested;

        if (Context.DebounceDuration != TimeSpan.Zero)
            SaveAndPublishStateDebounceDispatcher = new DebounceDispatcher(Context.DebounceDuration);

        CalculateHeatCopDebounceDispatcher = new DebounceDispatcher(TimeSpan.FromSeconds(180));
        CalculateHotWaterCopDebounceDispatcher = new DebounceDispatcher(TimeSpan.FromSeconds(180));

        await MqttSensors.CreateOrUpdateEntities();
        GetAndSanitizeState();
        MonitoringTask = Context.Scheduler.RunEvery(TimeSpan.FromSeconds(30), Context.Scheduler.Now, MonitorHeatPumpControls);
        await SaveAndPublishState();
    }

    private async void RoomTemperatureSensor_Changed(object? sender, NumericSensorEventArgs e)
    {
        try
        {
            await ResolveRoomEnergyDemand(e);
        }
        catch (Exception ex)
        {
            Context.Logger.LogError(ex, "Could not handle change of room temperature.");
        }
    }

    private async Task ResolveRoomEnergyDemand(NumericSensorEventArgs e)
    {
        if (e.New?.State == null)
            return;

        var roomTemperature = Convert.ToDecimal(e.New.State);

        var energyDemand = HeatPumpEnergyDemand.NoDemand;
        if (roomTemperature < TemperatureSettings.MinimumRoomTemperature)
            energyDemand = HeatPumpEnergyDemand.CriticalDemand;
        else if (roomTemperature < TemperatureSettings.ComfortRoomTemperature)
            energyDemand = HeatPumpEnergyDemand.Demanded;
        else if (roomTemperature > TemperatureSettings.MaximumRoomTemperature)
            energyDemand = HeatPumpEnergyDemand.NoDemand;

        if (energyDemand != State.RoomEnergyDemand)
            await SetEnergyDemand();
    }


    private async void HotWaterTemperatureSensor_Changed(object? sender, NumericSensorEventArgs e)
    {
        try
        {
            await ResolveHotWaterEnergyDemand(e);
        }
        catch (Exception ex)
        {
            Context.Logger.LogError(ex, "Could not handle change of water temperature.");
        }
    }

    private async Task ResolveHotWaterEnergyDemand(NumericSensorEventArgs e)
    {
        if (e.New?.State == null)
            return;

        var hotWaterTemperature = Convert.ToDecimal(e.New.State);

        var energyDemand = HeatPumpEnergyDemand.NoDemand;
        if (State.ShowerRequestedAt != null && hotWaterTemperature < TemperatureSettings.TargetShowerTemperature)
            energyDemand = HeatPumpEnergyDemand.CriticalDemand;
        else if (State.BathRequestedAt != null && hotWaterTemperature < TemperatureSettings.TargetBathTemperature)
            energyDemand = HeatPumpEnergyDemand.CriticalDemand;
        else if (hotWaterTemperature < TemperatureSettings.MinimumHotWaterTemperature)
            energyDemand = HeatPumpEnergyDemand.CriticalDemand;
        else if (hotWaterTemperature < TemperatureSettings.ComfortHotWaterTemperature)
            energyDemand = HeatPumpEnergyDemand.Demanded;
        else if (hotWaterTemperature > TemperatureSettings.MaximumHotWaterTemperature)
            energyDemand = HeatPumpEnergyDemand.NoDemand;

        var discardShowerRequested = State.ShowerRequestedAt != null && hotWaterTemperature >= TemperatureSettings.TargetShowerTemperature;
        var discardBathRequested = State.BathRequestedAt != null && hotWaterTemperature >= TemperatureSettings.TargetBathTemperature;

        if (discardShowerRequested || discardBathRequested)
            await DiscardBathAndShowerRequested(discardShowerRequested, discardBathRequested);

        if (energyDemand != State.HotWaterEnergyDemand)
            await SetEnergyDemand();
    }

    private async Task SetEnergyDemand()
    {
        if (State.RoomEnergyDemand is HeatPumpEnergyDemand.CriticalDemand || State.HotWaterEnergyDemand is HeatPumpEnergyDemand.CriticalDemand)
            State.EnergyDemand = HeatPumpEnergyDemand.CriticalDemand;
        else if (State.RoomEnergyDemand is HeatPumpEnergyDemand.Demanded || State.HotWaterEnergyDemand is HeatPumpEnergyDemand.Demanded)
            State.EnergyDemand = HeatPumpEnergyDemand.Demanded;
        else
            State.EnergyDemand = HeatPumpEnergyDemand.NoDemand;

        await DebounceSaveAndPublishState();
    }

    private async void OnShowerRequested(object? sender, EventArgs e)
    {
        try
        {
            State.ShowerRequestedAt = Context.Scheduler.Now;
            await DebounceSaveAndPublishState();
        }
        catch (Exception ex)
        {
            Context.Logger.LogError(ex, "Could not handle hot shower request.");
        }
    }

    private async Task OnBathRequested(object? sender, EventArgs e)
    {
        try
        {
            State.BathRequestedAt = Context.Scheduler.Now;
            await DebounceSaveAndPublishState();
        }
        catch (Exception ex)
        {
            Context.Logger.LogError(ex, "Could not handle hot bath request.");
        }
    }

    private async void MonitorHeatPumpControls()
    {
        try
        {
            await DiscardBathAndShowerRequested();
        }
        catch (Exception ex)
        {
            Context.Logger.LogError(ex, "Could monitor heat pump controls");
        }
    }

    private async Task DiscardBathAndShowerRequested(bool forceDiscardShowerRequested = false, bool forceDiscardBathRequested = false)
    {
        var changed = false;
        if (forceDiscardShowerRequested || (State.ShowerRequestedAt != null && State.ShowerRequestedAt.Value.AddHours(3) < Context.Scheduler.Now))
        {
            State.ShowerRequestedAt = null;
            changed = true;
        }
        if (forceDiscardBathRequested || (State.BathRequestedAt != null && State.BathRequestedAt.Value.AddHours(3) < Context.Scheduler.Now))
        {
            State.BathRequestedAt = null;
            changed = true;
        }

        if (changed)
            await DebounceSaveAndPublishState();
    }

    private async void SmartGridReadyModeChangedEvent(object? sender, SmartGridReadyModeChangedEventArgs e)
    {
        try
        {
            Context.Logger.LogInformation($"Setting smart grid ready mode to {e.SmartGridReadyMode}.");
            State.SmartGridReadyMode = e.SmartGridReadyMode;
            await SetSmartGridReadyInputs();
        }
        catch (Exception ex)
        {
            Context.Logger.LogError(ex, "Could not handle Smart grid ready mode state change.");
        }
    }

    private async void HeatSensor_changed(object? sender, NumericSensorEventArgs e)
    {
        try
        {
            await CalculateHeatCopDebounceDispatcher.DebounceAsync(CalculateHeatCoefficientOfPerformance);
        }
        catch (Exception ex)
        {
            Context.Logger.LogError(ex, "Could not calculate coefficient of performance for heat.");
        }
    }

    private async Task CalculateHeatCoefficientOfPerformance()
    {
        var cop = CalculateCoefficientOfPerformance(HomeAssistant.HeatConsumedTodayIntegerSensor.State, HomeAssistant.HeatConsumedTodayDecimalsSensor.State, HomeAssistant.HeatProducedTodayIntegerSensor.State, HomeAssistant.HeatProducedTodayDecimalsSensor.State);

        if (cop != null)
            State.HeatCoefficientOfPerformance = cop.Value;
        await DebounceSaveAndPublishState();
    }


    private async void HotWaterSensor_changed(object? sender, NumericSensorEventArgs e)
    {
        try
        {
            await CalculateHotWaterCopDebounceDispatcher.DebounceAsync(CalculateHotWaterCoefficientOfPerformance);
        }
        catch (Exception ex)
        {
            Context.Logger.LogError(ex, "Could not calculate coefficient of performance for hot water.");
        }
    }

    private async Task CalculateHotWaterCoefficientOfPerformance()
    {
        var cop = CalculateCoefficientOfPerformance(HomeAssistant.HotWaterConsumedTodayIntegerSensor.State, HomeAssistant.HotWaterConsumedTodayDecimalsSensor.State, HomeAssistant.HotWaterProducedTodayIntegerSensor.State, HomeAssistant.HotWaterProducedTodayDecimalsSensor.State);

        if (cop != null)
            State.HotWaterCoefficientOfPerformance = cop.Value;
        await DebounceSaveAndPublishState();
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
            if (e.Old?.State == Constants.UNAVAILABLE && e.New?.State != Constants.UNAVAILABLE)
            {
                Context.Logger.LogDebug("Smart heat pump: ISG was not available for a while but came back online, we can assume it rebooted.");
                await SetSmartGridReadyInputs();
            }
        }
        catch (Exception ex)
        {
            Context.Logger.LogError(ex, "Could not set SG ready state.");
        }
    }

    private async void SourceTemperatureSensor_Changed(object? sender, NumericSensorEventArgs e)
    {
        try
        {
            if (State.SourcePumpStartedAt == null)
                return;

            if (State.SourcePumpStartedAt.Value.AddMinutes(15) > Scheduler.Now)
                return;

            if (e.New?.State != null)
            {
                UpdateSourceTemperature(e.New.State.Value, HomeAssistant.SourcePumpRunningSensor.IsOn(), HomeAssistant.IsCoolingSensor.IsOn());
                await DebounceSaveAndPublishState();
            }
        }
        catch (Exception ex)
        {
            Context.Logger.LogError(ex, "Could not handle change of source temperature.");
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
            Context.Logger.LogError(ex, "Could not handle source pump turned on event.");
        }
    }

    private async void SourcePumpRunningSensor_TurnedOff(object? sender, BinarySensorEventArgs e)
    {
        try
        {
            if (State.SourcePumpStartedAt?.AddMinutes(15) <= Scheduler.Now && HomeAssistant.SourceTemperatureSensor.State != null)
                UpdateSourceTemperature(HomeAssistant.SourceTemperatureSensor.State.Value, false, HomeAssistant.IsCoolingSensor.IsOn());

            State.SourcePumpStartedAt = null;
            await DebounceSaveAndPublishState();
        }
        catch (Exception ex)
        {
            Context.Logger.LogError(ex, "Could not handle source pump turned off event.");
        }
    }

    private void UpdateSourceTemperature(double temperature, bool sourcePumpRunning, bool isCooling)
    {
        if (isCooling)
            return;

        var maxAllowedVariation = 1.0;
        var difference = temperature - State.SourceTemperature;

        var updateSourceTemperature = sourcePumpRunning switch
        {
            false when State.SourceTemperatureUpdatedAt > State.SourcePumpStartedAt => false,
            false when difference < maxAllowedVariation => true,
            true when State.SourceTemperatureUpdatedAt < State.SourcePumpStartedAt => true,
            true when difference < maxAllowedVariation => true,
            _ when State.SourceTemperatureUpdatedAt == null => true,
            _ => false,
        };

        if (!updateSourceTemperature)
        {
            Context.Logger.LogTrace($"Did not update source temperature. Current source temperature was '{State.SourceTemperature} °C', new temperature was '{temperature} °C'. (Δ={Math.Round(difference, 1)} °C). Last update was: {State.SourceTemperatureUpdatedAt:O}.");
            return;
        }

        State.SourceTemperature = temperature;
        State.SourceTemperatureUpdatedAt = Scheduler.Now;
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

    private void GetAndSanitizeState()
    {
        var persistedState = Context.FileStorage.Get<SmartHeatPumpState>("SmartHeatPump", "SmartHeatPump");

        State = persistedState ?? new SmartHeatPumpState();

        if (HomeAssistant.SourcePumpRunningSensor.IsOn() && State.SourcePumpStartedAt == null)
            State.SourcePumpStartedAt = Scheduler.Now;

        if (HomeAssistant.SourcePumpRunningSensor.IsOff() && State.SourcePumpStartedAt != null)
            State.SourcePumpStartedAt = null;

        State.HeatCoefficientOfPerformance ??= CalculateCoefficientOfPerformance(HomeAssistant.HeatConsumedTodayIntegerSensor.State, HomeAssistant.HeatConsumedTodayDecimalsSensor.State, HomeAssistant.HeatProducedTodayIntegerSensor.State, HomeAssistant.HeatProducedTodayDecimalsSensor.State);
        State.HotWaterCoefficientOfPerformance ??= CalculateCoefficientOfPerformance(HomeAssistant.HotWaterConsumedTodayIntegerSensor.State, HomeAssistant.HotWaterConsumedTodayDecimalsSensor.State, HomeAssistant.HotWaterProducedTodayIntegerSensor.State, HomeAssistant.HotWaterProducedTodayDecimalsSensor.State);

        Context.Logger.LogDebug("Retrieved Smart heat pump state.");
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

    private async Task SaveAndPublishState()
    {
        Context.FileStorage.Save("SmartHeatPump", "SmartHeatPump", State);
        await MqttSensors.PublishState(State);
    }

    public void Dispose()
    {
        MqttSensors.SmartGridReadyModeChanged -= SmartGridReadyModeChangedEvent;
        MqttSensors.Dispose();

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
    }
}