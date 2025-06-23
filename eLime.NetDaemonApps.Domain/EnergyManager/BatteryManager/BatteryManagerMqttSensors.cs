using eLime.NetDaemonApps.Domain.Mqtt;
using NetDaemon.Extensions.MqttEntityManager;
using System.Globalization;

namespace eLime.NetDaemonApps.Domain.EnergyManager.BatteryManager;

#pragma warning disable CS8618, CS9264, CS8604
public class BatteryManagerMqttSensors : IDisposable
{
    private readonly string SENSOR_BATTERY_MANAGER_REMAINING_AVAILABLE_CAPACITY;
    private readonly string SENSOR_BATTERY_MANAGER_STATE_OF_CHARGE;

    protected Device Device;
    protected readonly EnergyManagerContext Context;

    public BatteryManagerMqttSensors(EnergyManagerContext context)
    {
        Context = context;

        Device = new Device { Identifiers = ["battery_manager"], Name = "Battery manager", Manufacturer = "Me" };
        SENSOR_BATTERY_MANAGER_REMAINING_AVAILABLE_CAPACITY = "sensor.battery_manager_remaining_available_capacity";
        SENSOR_BATTERY_MANAGER_STATE_OF_CHARGE = "sensor.battery_manager_aggregate_soc";
    }

    internal virtual async Task CreateOrUpdateEntities()
    {
        var currentCapacityCreationOptions = new EntityCreationOptions(UniqueId: SENSOR_BATTERY_MANAGER_REMAINING_AVAILABLE_CAPACITY, Name: "Remaining available capacity", Persist: true);
        var currentCapacityOptions = new NumericSensorOptions { Icon = "phu:solar-battery-10", Device = Device, UnitOfMeasurement = "kWh" };
        await Context.MqttEntityManager.CreateAsync(SENSOR_BATTERY_MANAGER_REMAINING_AVAILABLE_CAPACITY, currentCapacityCreationOptions, currentCapacityOptions);

        var socCreationOptions = new EntityCreationOptions(UniqueId: SENSOR_BATTERY_MANAGER_STATE_OF_CHARGE, Name: "Aggregate SoC", DeviceClass: "battery", Persist: true);
        var socOptions = new NumericSensorOptions { Icon = "fapro:percent", Device = Device, StateClass = "measurement", UnitOfMeasurement = "%" };
        await Context.MqttEntityManager.CreateAsync(SENSOR_BATTERY_MANAGER_STATE_OF_CHARGE, socCreationOptions, socOptions);
    }

    internal virtual async Task PublishState(BatteryManagerState state)
    {
        await Context.MqttEntityManager.SetStateAsync(SENSOR_BATTERY_MANAGER_REMAINING_AVAILABLE_CAPACITY, state.RemainingAvailableCapacity.ToString("N", new NumberFormatInfo { NumberDecimalSeparator = "." }));
        await Context.MqttEntityManager.SetStateAsync(SENSOR_BATTERY_MANAGER_STATE_OF_CHARGE, state.StateOfCharge.ToString());
    }

    public virtual void Dispose()
    {
        // TODO release managed resources here
    }
}