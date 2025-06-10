using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Domain.Mqtt;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.HassModel;
using System.Globalization;

namespace eLime.NetDaemonApps.Domain.SmartHeatPump;

#pragma warning disable CS8618, CS9264
public class SmartHeatPumpEntities(IMqttEntityManager mqttEntityManager) : IDisposable
{
    private const string smartGridReadyModeSelectName = "select.heat_pump_smart_grid_ready_mode";
    private const string sourceTemperatureSensorName = "sensor.heat_pump_source_temperature";

    private const string heatConsumedTodaySensorName = "sensor.heat_pump_heat_consumed_today";
    private const string heatProducedTodaySensorName = "sensor.heat_pump_heat_produced_today";
    private const string hotWaterConsumedTodaySensorName = "sensor.heat_pump_hot_water_consumed_today";
    private const string hotWaterProducedTodaySensorName = "sensor.heat_pump_hot_water_produced_today";

    private const string heatCoefficientOfPerformanceSensorName = "sensor.heat_pump_heat_coefficient_of_performance";
    private const string hotWaterCoefficientOfPerformanceSensorName = "sensor.heat_pump_hot_water_coefficient_of_performance";

    public event EventHandler<SmartGridReadyModeChangedEventArgs>? SmartGridReadyModeChangedEvent;
    private void OnSmartGridReadyModeChanged(SmartGridReadyModeChangedEventArgs e)
    {
        SmartGridReadyModeChangedEvent?.Invoke(this, e);
    }
    private IDisposable? SmartGridReadyModeChangedEventHandlerObservable { get; set; }

    internal async Task Publish()
    {
        var smartGridReadySelectOptions = new SelectOptions
        {
            Icon = "phu:smarthome-solver",
            Options = Enum<SmartGridReadyMode>.AllValuesAsStringList(),
            Device = GetDevice()
        };

        await mqttEntityManager.CreateAsync(smartGridReadyModeSelectName, new EntityCreationOptions(UniqueId: smartGridReadyModeSelectName, Name: "Smart grid ready mode", DeviceClass: "select", Persist: true), smartGridReadySelectOptions);
        var smartGridReadyModeObservable = await mqttEntityManager.PrepareCommandSubscriptionAsync(smartGridReadyModeSelectName);
        SmartGridReadyModeChangedEventHandlerObservable = smartGridReadyModeObservable.SubscribeAsync(SmartGridReadyModeChangedEventHandler());

        var sourceTemperatureSensorOptions = new NumericSensorOptions
        {
            StateClass = "measurement",
            UnitOfMeasurement = "°C",
            Icon = "fapro:oil-temperature",
            Device = GetDevice()
        };
        await mqttEntityManager.CreateAsync(sourceTemperatureSensorName, new EntityCreationOptions(UniqueId: sourceTemperatureSensorName, Name: "Source temperature", DeviceClass: "temperature", Persist: true), sourceTemperatureSensorOptions);

        //Not sure yet if I want to expose these sensors
        //var energySensorOptions = new NumericSensorOptions
        //{
        //    StateClass = "total",
        //    UnitOfMeasurement = "kWh",
        //    Icon = "fapro:meter-bolt",
        //    Device = GetDevice()
        //};
        //await mqttEntityManager.CreateAsync(heatConsumedTodaySensorName, new EntityCreationOptions(UniqueId: heatConsumedTodaySensorName, Name: "Heat consumed today", DeviceClass: "energy", Persist: true), energySensorOptions);
        //await mqttEntityManager.CreateAsync(heatProducedTodaySensorName, new EntityCreationOptions(UniqueId: heatProducedTodaySensorName, Name: "Heat produced today", DeviceClass: "energy", Persist: true), energySensorOptions);
        //await mqttEntityManager.CreateAsync(hotWaterConsumedTodaySensorName, new EntityCreationOptions(UniqueId: hotWaterConsumedTodaySensorName, Name: "Hot water consumed today", DeviceClass: "energy", Persist: true), energySensorOptions);
        //await mqttEntityManager.CreateAsync(hotWaterProducedTodaySensorName, new EntityCreationOptions(UniqueId: hotWaterProducedTodaySensorName, Name: "Hot water produced today", DeviceClass: "energy", Persist: true), energySensorOptions);

        var coefficientOfPerformanceSensorOptions = new NumericSensorOptions
        {
            StateClass = "measurement",
            UnitOfMeasurement = "",
            Icon = "fapro:chart-line",
            Device = GetDevice()
        };
        await mqttEntityManager.CreateAsync(heatCoefficientOfPerformanceSensorName, new EntityCreationOptions(UniqueId: heatCoefficientOfPerformanceSensorName, Name: "Heat coefficient of performance", Persist: true), coefficientOfPerformanceSensorOptions);
        await mqttEntityManager.CreateAsync(hotWaterCoefficientOfPerformanceSensorName, new EntityCreationOptions(UniqueId: hotWaterCoefficientOfPerformanceSensorName, Name: "Hot water coefficient of performance", Persist: true), coefficientOfPerformanceSensorOptions);
    }
    private Func<string, Task> SmartGridReadyModeChangedEventHandler()
    {
        return state =>
        {
            OnSmartGridReadyModeChanged(SmartGridReadyModeChangedEventArgs.Create(Enum<SmartGridReadyMode>.Cast(state)));
            return Task.CompletedTask;
        };
    }

    private Device GetDevice()
    {
        return new Device { Identifiers = ["smart_heat_pump"], Name = "Smart heat pump", Manufacturer = "Me" };
    }

    internal async Task PublishState(SmartHeatPumpState state)
    {
        //var globalAttributes = new EnergyManagerAttributes()
        //{
        //    LastUpdated = DateTime.Now.ToString("O"),
        //};

        await mqttEntityManager.SetStateAsync(smartGridReadyModeSelectName, state.SmartGridReadyMode.ToString());
        await mqttEntityManager.SetStateAsync(sourceTemperatureSensorName, state.SourceTemperature.ToString("N", new NumberFormatInfo { NumberDecimalSeparator = "." }));

        //await mqttEntityManager.SetStateAsync(heatConsumedTodaySensorName, state.HeatConsumedToday.ToString("N", new NumberFormatInfo { NumberDecimalSeparator = "." }));
        //await mqttEntityManager.SetStateAsync(heatProducedTodaySensorName, state.HeatProducedToday.ToString("N", new NumberFormatInfo { NumberDecimalSeparator = "." }));
        //await mqttEntityManager.SetStateAsync(hotWaterConsumedTodaySensorName, state.HotWaterConsumedToday.ToString("N", new NumberFormatInfo { NumberDecimalSeparator = "." }));
        //await mqttEntityManager.SetStateAsync(hotWaterProducedTodaySensorName, state.HotWaterProducedToday.ToString("N", new NumberFormatInfo { NumberDecimalSeparator = "." }));

        await mqttEntityManager.SetStateAsync(heatCoefficientOfPerformanceSensorName, state.HeatCoefficientOfPerformance?.ToString("N", new NumberFormatInfo { NumberDecimalSeparator = "." }) ?? "");
        await mqttEntityManager.SetStateAsync(hotWaterCoefficientOfPerformanceSensorName, state.HotWaterCoefficientOfPerformance?.ToString("N", new NumberFormatInfo { NumberDecimalSeparator = "." }) ?? "");
        //await _mqttEntityManager.SetAttributesAsync("sensor.energy_manager_state", globalAttributes);
    }

    public void Dispose()
    {
        SmartGridReadyModeChangedEventHandlerObservable?.Dispose();
    }

}