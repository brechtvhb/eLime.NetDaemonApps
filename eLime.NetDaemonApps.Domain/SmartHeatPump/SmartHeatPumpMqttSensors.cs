using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Domain.Mqtt;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.HassModel;
using System.Globalization;

namespace eLime.NetDaemonApps.Domain.SmartHeatPump;

#pragma warning disable CS8618, CS9264
public class SmartHeatPumpMqttSensors : IDisposable
{
    protected readonly SmartHeatPumpContext Context;

    private readonly string SELECT_SMART_GRID_READY_MODE;

    private readonly string SENSOR_ENERGY_DEMAND;

    private readonly string BUTTON_REQUEST_SHOWER;
    private readonly string BUTTON_REQUEST_BATH;
    private readonly string BINARY_SENSOR_BATH_REQUESTED;
    private readonly string BINARY_SENSOR_SHOWER_REQUESTED;

    private readonly string SENSOR_HEAT_PUMP_SOURCE_TEMPERATURE;
    private readonly string SENSOR_HEAT_PUMP_HEAT_COEFFICIENT_OF_PERFORMANCE;
    private readonly string SENSOR_HEAT_PUMP_HOT_WATER_COEFFICIENT_OF_PERFORMANCE;

    private readonly string SENSOR_EXPECTED_POWER_CONSUMPTION;
    private readonly Device Device;

    public SmartHeatPumpMqttSensors(SmartHeatPumpContext context)
    {
        Context = context;
        Device = new Device { Identifiers = ["smart_heat_pump"], Name = "Smart heat pump", Manufacturer = "Me" };
        SELECT_SMART_GRID_READY_MODE = "select.heat_pump_smart_grid_ready_mode";

        SENSOR_ENERGY_DEMAND = "sensor.heat_pump_energy_demand";

        BUTTON_REQUEST_SHOWER = "button.heat_pump_request_shower";
        BUTTON_REQUEST_BATH = "button.heat_pump_request_bath";
        BINARY_SENSOR_BATH_REQUESTED = "binary_sensor.heat_pump_bath_requested";
        BINARY_SENSOR_SHOWER_REQUESTED = "binary_sensor.heat_pump_shower_requested";

        SENSOR_HEAT_PUMP_SOURCE_TEMPERATURE = "sensor.heat_pump_source_temperature";
        SENSOR_HEAT_PUMP_HEAT_COEFFICIENT_OF_PERFORMANCE = "sensor.heat_pump_heat_coefficient_of_performance";
        SENSOR_HEAT_PUMP_HOT_WATER_COEFFICIENT_OF_PERFORMANCE = "sensor.heat_pump_hot_water_coefficient_of_performance";

        SENSOR_EXPECTED_POWER_CONSUMPTION = "sensor.heat_pump_expected_power_consumption";
    }

    public event EventHandler<SmartGridReadyModeChangedEventArgs>? SmartGridReadyModeChanged;
    public event EventHandler<EventArgs> ShowerRequested;
    public event EventHandler<EventArgs> BathRequested;

    private void OnSmartGridReadyModeChanged(SmartGridReadyModeChangedEventArgs e)
    {
        SmartGridReadyModeChanged?.Invoke(this, e);
    }

    private void OnShowerRequested(EventArgs e)
    {
        ShowerRequested?.Invoke(this, e);
    }

    private void OnBathRequested(EventArgs e)
    {
        BathRequested?.Invoke(this, e);
    }

    private IDisposable? SmartGridReadyModeObservable { get; set; }
    private IDisposable? RequestShowerObservable { get; set; }
    private IDisposable? RequestBathObservable { get; set; }

    internal async Task CreateOrUpdateEntities()
    {
        var smartGridReadyCreationOptions = new EntityCreationOptions(UniqueId: SELECT_SMART_GRID_READY_MODE, Name: "Smart grid ready mode", DeviceClass: "select", Persist: true);
        var smartGridReadySelectOptions = new SelectOptions { Icon = "phu:smarthome-solver", Options = Enum<SmartGridReadyMode>.AllValuesAsStringList(), Device = Device };
        await Context.MqttEntityManager.CreateAsync(SELECT_SMART_GRID_READY_MODE, smartGridReadyCreationOptions, smartGridReadySelectOptions);
        var smartGridReadyModeObservable = await Context.MqttEntityManager.PrepareCommandSubscriptionAsync(SELECT_SMART_GRID_READY_MODE);
        SmartGridReadyModeObservable = smartGridReadyModeObservable.SubscribeAsync(SmartGridReadyModeChangedEventHandler());

        var energyDemandCreationOptions = new EntityCreationOptions(DeviceClass: "enum", UniqueId: SENSOR_ENERGY_DEMAND, Name: $"Energy demand", Persist: true);
        var energyDemandOptions = new EnumSensorOptions { Icon = "fapro-duotone:circle-bolt", Device = Device, Options = Enum<HeatPumpEnergyDemand>.AllValuesAsStringList() };
        await Context.MqttEntityManager.CreateAsync(SENSOR_ENERGY_DEMAND, energyDemandCreationOptions, energyDemandOptions);

        var requestShowerCreationOptions = new EntityCreationOptions(UniqueId: BUTTON_REQUEST_SHOWER, Name: "Request shower", Persist: true);
        var requestShowerButtonOptions = new ButtonOptions { Icon = "fapro-duotone:shower", Device = Device, PayloadPress = "REQUEST" };
        await Context.MqttEntityManager.CreateAsync(BUTTON_REQUEST_SHOWER, requestShowerCreationOptions, requestShowerButtonOptions);
        var requestShowerObservable = await Context.MqttEntityManager.PrepareCommandSubscriptionAsync(BUTTON_REQUEST_SHOWER);
        RequestShowerObservable = requestShowerObservable.SubscribeAsync(RequestShowerTriggeredHandler());

        var requestBathCreationOptions = new EntityCreationOptions(UniqueId: BUTTON_REQUEST_BATH, Name: "Request bath", Persist: true);
        var requestBathButtonOptions = new ButtonOptions { Icon = "fapro-duotone:bath", Device = Device, PayloadPress = "REQUEST" };
        await Context.MqttEntityManager.CreateAsync(BUTTON_REQUEST_BATH, requestBathCreationOptions, requestBathButtonOptions);
        var requestBathObservable = await Context.MqttEntityManager.PrepareCommandSubscriptionAsync(BUTTON_REQUEST_BATH);
        RequestBathObservable = requestBathObservable.SubscribeAsync(RequestBathTriggeredHandler());

        var bathRequestedCreationOptions = new EntityCreationOptions(UniqueId: BINARY_SENSOR_BATH_REQUESTED, Name: "Bath requested", DeviceClass: "running", Persist: true);
        var bathRequestedSensorOptions = new EntityOptions { Icon = "fapro-duotone:bath", Device = Device };
        await Context.MqttEntityManager.CreateAsync(BINARY_SENSOR_BATH_REQUESTED, bathRequestedCreationOptions, bathRequestedSensorOptions);

        var showerRequestedCreationOptions = new EntityCreationOptions(UniqueId: BINARY_SENSOR_SHOWER_REQUESTED, Name: "Shower requested", DeviceClass: "running", Persist: true);
        var showerRequestedSensorOptions = new EntityOptions { Icon = "fapro-duotone:shower", Device = Device };
        await Context.MqttEntityManager.CreateAsync(BINARY_SENSOR_SHOWER_REQUESTED, showerRequestedCreationOptions, showerRequestedSensorOptions);

        var sourceTemperatureCreationOptions = new EntityCreationOptions(UniqueId: SENSOR_HEAT_PUMP_SOURCE_TEMPERATURE, Name: "Source temperature", DeviceClass: "temperature", Persist: true);
        var sourceTemperatureSensorOptions = new NumericSensorOptions { StateClass = "measurement", UnitOfMeasurement = "°C", Icon = "fapro-duotone:oil-temperature", Device = Device };
        await Context.MqttEntityManager.CreateAsync(SENSOR_HEAT_PUMP_SOURCE_TEMPERATURE, sourceTemperatureCreationOptions, sourceTemperatureSensorOptions);

        var coefficientOfPerformanceSensorOptions = new NumericSensorOptions { StateClass = "measurement", UnitOfMeasurement = "", Icon = "fapro-duotone:chart-line", Device = Device };
        var heatCopCreationOptions = new EntityCreationOptions(UniqueId: SENSOR_HEAT_PUMP_HEAT_COEFFICIENT_OF_PERFORMANCE, Name: "Heat coefficient of performance", Persist: true);
        await Context.MqttEntityManager.CreateAsync(SENSOR_HEAT_PUMP_HEAT_COEFFICIENT_OF_PERFORMANCE, heatCopCreationOptions, coefficientOfPerformanceSensorOptions);
        var hotWaterCopCreationOptions = new EntityCreationOptions(UniqueId: SENSOR_HEAT_PUMP_HOT_WATER_COEFFICIENT_OF_PERFORMANCE, Name: "Hot water coefficient of performance", Persist: true);
        await Context.MqttEntityManager.CreateAsync(SENSOR_HEAT_PUMP_HOT_WATER_COEFFICIENT_OF_PERFORMANCE, hotWaterCopCreationOptions, coefficientOfPerformanceSensorOptions);

        var expectedPowerConsumptionCreationOptions = new EntityCreationOptions(UniqueId: SENSOR_EXPECTED_POWER_CONSUMPTION, Name: "Expected power consumption", DeviceClass: "power", Persist: true);
        var expectedPowerConsumptionSensorOptions = new NumericSensorOptions { UnitOfMeasurement = "W", Icon = "fapro-duotone:bolt-lightning", Device = Device };
        await Context.MqttEntityManager.CreateAsync(SENSOR_EXPECTED_POWER_CONSUMPTION, expectedPowerConsumptionCreationOptions, expectedPowerConsumptionSensorOptions);

    }
    private Func<string, Task> SmartGridReadyModeChangedEventHandler()
    {
        return state =>
        {
            OnSmartGridReadyModeChanged(SmartGridReadyModeChangedEventArgs.Create(Enum<SmartGridReadyMode>.Cast(state)));
            return Task.CompletedTask;
        };
    }

    private Func<string, Task> RequestShowerTriggeredHandler()
    {
        return state =>
        {
            Context.Logger.LogDebug("Smart heat pump: Shower requested");

            if (state == "REQUEST")
                OnShowerRequested(EventArgs.Empty);

            return Task.CompletedTask;
        };
    }
    private Func<string, Task> RequestBathTriggeredHandler()
    {
        return state =>
        {
            Context.Logger.LogDebug("Smart heat pump: Bath requested");

            if (state == "REQUEST")
                OnBathRequested(EventArgs.Empty);

            return Task.CompletedTask;
        };
    }

    internal async Task PublishState(SmartHeatPumpState state)
    {
        await Context.MqttEntityManager.SetStateAsync(SELECT_SMART_GRID_READY_MODE, state.SmartGridReadyMode.ToString());
        await Context.MqttEntityManager.SetStateAsync(SENSOR_ENERGY_DEMAND, state.EnergyDemand.ToString());

        await Context.MqttEntityManager.SetStateAsync(BINARY_SENSOR_SHOWER_REQUESTED, state.ShowerRequestedAt != null ? "ON" : "OFF");
        await Context.MqttEntityManager.SetStateAsync(BINARY_SENSOR_BATH_REQUESTED, state.BathRequestedAt != null ? "ON" : "OFF");

        await Context.MqttEntityManager.SetStateAsync(SENSOR_HEAT_PUMP_SOURCE_TEMPERATURE, state.SourceTemperature.ToString("N", new NumberFormatInfo { NumberDecimalSeparator = "." }));
        await Context.MqttEntityManager.SetStateAsync(SENSOR_HEAT_PUMP_HEAT_COEFFICIENT_OF_PERFORMANCE, state.HeatCoefficientOfPerformance?.ToString("N", new NumberFormatInfo { NumberDecimalSeparator = "." }) ?? "");
        await Context.MqttEntityManager.SetStateAsync(SENSOR_HEAT_PUMP_HOT_WATER_COEFFICIENT_OF_PERFORMANCE, state.HotWaterCoefficientOfPerformance?.ToString("N", new NumberFormatInfo { NumberDecimalSeparator = "." }) ?? "");

        await Context.MqttEntityManager.SetStateAsync(SENSOR_EXPECTED_POWER_CONSUMPTION, state.ExpectedPowerConsumption.ToString("F0", CultureInfo.InvariantCulture));
    }

    public void Dispose()
    {
        SmartGridReadyModeObservable?.Dispose();
        RequestShowerObservable?.Dispose();
        RequestBathObservable?.Dispose();
    }

}