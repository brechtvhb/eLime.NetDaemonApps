using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Domain.Mqtt;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.HassModel;
using System.Globalization;

namespace eLime.NetDaemonApps.Domain.SmartHeatPump;

#pragma warning disable CS8618, CS9264
public class SmartHeatPumpMqttSensors : IDisposable
{
    protected readonly SmartHeatPumpContext Context;

    private readonly string SELECT_SMART_GRID_READY_MODE;
    private readonly string SENSOR_HEAT_PUMP_SOURCE_TEMPERATURE;
    private readonly string SENSOR_HEAT_PUMP_HEAT_COEFFICIENT_OF_PERFORMANCE;
    private readonly string SENSOR_HEAT_PUMP_HOT_WATER_COEFFICIENT_OF_PERFORMANCE;
    private Device Device;

    public SmartHeatPumpMqttSensors(SmartHeatPumpContext context)
    {
        Context = context;
        Device = new Device { Identifiers = ["smart_heat_pump"], Name = "Smart heat pump", Manufacturer = "Me" };
        SELECT_SMART_GRID_READY_MODE = "select.heat_pump_smart_grid_ready_mode";
        SENSOR_HEAT_PUMP_SOURCE_TEMPERATURE = "sensor.heat_pump_source_temperature";
        SENSOR_HEAT_PUMP_HEAT_COEFFICIENT_OF_PERFORMANCE = "sensor.heat_pump_heat_coefficient_of_performance";
        SENSOR_HEAT_PUMP_HOT_WATER_COEFFICIENT_OF_PERFORMANCE = "sensor.heat_pump_hot_water_coefficient_of_performance";

    }

    public event EventHandler<SmartGridReadyModeChangedEventArgs>? SmartGridReadyModeChangedEvent;
    private void OnSmartGridReadyModeChanged(SmartGridReadyModeChangedEventArgs e)
    {
        SmartGridReadyModeChangedEvent?.Invoke(this, e);
    }
    private IDisposable? SmartGridReadyModeChangedEventHandlerObservable { get; set; }

    internal async Task CreateOrUpdateEntities()
    {
        var smartGridReadyCreationOptions = new EntityCreationOptions(UniqueId: SELECT_SMART_GRID_READY_MODE, Name: "Smart grid ready mode", DeviceClass: "select", Persist: true);
        var smartGridReadySelectOptions = new SelectOptions { Icon = "phu:smarthome-solver", Options = Enum<SmartGridReadyMode>.AllValuesAsStringList(), Device = Device };
        await Context.MqttEntityManager.CreateAsync(SELECT_SMART_GRID_READY_MODE, smartGridReadyCreationOptions, smartGridReadySelectOptions);

        var smartGridReadyModeObservable = await Context.MqttEntityManager.PrepareCommandSubscriptionAsync(SELECT_SMART_GRID_READY_MODE);
        SmartGridReadyModeChangedEventHandlerObservable = smartGridReadyModeObservable.SubscribeAsync(SmartGridReadyModeChangedEventHandler());

        var sourceTemperatureCreationOptions = new EntityCreationOptions(UniqueId: SENSOR_HEAT_PUMP_SOURCE_TEMPERATURE, Name: "Source temperature", DeviceClass: "temperature", Persist: true);
        var sourceTemperatureSensorOptions = new NumericSensorOptions { StateClass = "measurement", UnitOfMeasurement = "°C", Icon = "fapro-duotone:oil-temperature", Device = Device };
        await Context.MqttEntityManager.CreateAsync(SENSOR_HEAT_PUMP_SOURCE_TEMPERATURE, sourceTemperatureCreationOptions, sourceTemperatureSensorOptions);

        var coefficientOfPerformanceSensorOptions = new NumericSensorOptions { StateClass = "measurement", UnitOfMeasurement = "", Icon = "fapro-duotone:chart-line", Device = Device };

        var heatCopCreationOptions = new EntityCreationOptions(UniqueId: SENSOR_HEAT_PUMP_HEAT_COEFFICIENT_OF_PERFORMANCE, Name: "Heat coefficient of performance", Persist: true);
        await Context.MqttEntityManager.CreateAsync(SENSOR_HEAT_PUMP_HEAT_COEFFICIENT_OF_PERFORMANCE, heatCopCreationOptions, coefficientOfPerformanceSensorOptions);

        var hotWaterCopCreationOptions = new EntityCreationOptions(UniqueId: SENSOR_HEAT_PUMP_HOT_WATER_COEFFICIENT_OF_PERFORMANCE, Name: "Hot water coefficient of performance", Persist: true);
        await Context.MqttEntityManager.CreateAsync(SENSOR_HEAT_PUMP_HOT_WATER_COEFFICIENT_OF_PERFORMANCE, hotWaterCopCreationOptions, coefficientOfPerformanceSensorOptions);
    }
    private Func<string, Task> SmartGridReadyModeChangedEventHandler()
    {
        return state =>
        {
            OnSmartGridReadyModeChanged(SmartGridReadyModeChangedEventArgs.Create(Enum<SmartGridReadyMode>.Cast(state)));
            return Task.CompletedTask;
        };
    }

    internal async Task PublishState(SmartHeatPumpState state)
    {
        await Context.MqttEntityManager.SetStateAsync(SELECT_SMART_GRID_READY_MODE, state.SmartGridReadyMode.ToString());
        await Context.MqttEntityManager.SetStateAsync(SENSOR_HEAT_PUMP_SOURCE_TEMPERATURE, state.SourceTemperature.ToString("N", new NumberFormatInfo { NumberDecimalSeparator = "." }));

        await Context.MqttEntityManager.SetStateAsync(SENSOR_HEAT_PUMP_HEAT_COEFFICIENT_OF_PERFORMANCE, state.HeatCoefficientOfPerformance?.ToString("N", new NumberFormatInfo { NumberDecimalSeparator = "." }) ?? "");
        await Context.MqttEntityManager.SetStateAsync(SENSOR_HEAT_PUMP_HOT_WATER_COEFFICIENT_OF_PERFORMANCE, state.HotWaterCoefficientOfPerformance?.ToString("N", new NumberFormatInfo { NumberDecimalSeparator = "." }) ?? "");
    }

    public void Dispose()
    {
        SmartGridReadyModeChangedEventHandlerObservable?.Dispose();
    }

}