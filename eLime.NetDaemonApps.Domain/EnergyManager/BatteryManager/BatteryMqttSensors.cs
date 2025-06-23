using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Domain.Mqtt;
using NetDaemon.Extensions.MqttEntityManager;
using System.Globalization;

namespace eLime.NetDaemonApps.Domain.EnergyManager.BatteryManager;

#pragma warning disable CS8618, CS9264, CS8604
public class BatteryMqttSensors : IDisposable
{
    private readonly string SENSOR_BATTERY_RTE;

    protected Device Device;
    protected readonly EnergyManagerContext Context;
    internal readonly string Name;

    public BatteryMqttSensors(string name, EnergyManagerContext context)
    {
        Context = context;

        Name = name;
        Device = new Device { Identifiers = [$"battery_{Name.MakeHaFriendly()}"], Name = "Battery: " + Name, Manufacturer = "Me" };
        SENSOR_BATTERY_RTE = $"sensor.battery_{Name.MakeHaFriendly()}_rte";
    }

    internal virtual async Task CreateOrUpdateEntities()
    {
        var stateCreationOptions = new EntityCreationOptions(UniqueId: SENSOR_BATTERY_RTE, Name: $"Battery {Name} - RTE", Persist: true);
        var stateOptions = new NumericSensorOptions { Icon = "fapro:percent", Device = Device, StateClass = "measurement", UnitOfMeasurement = "" };
        await Context.MqttEntityManager.CreateAsync(SENSOR_BATTERY_RTE, stateCreationOptions, stateOptions);
    }

    internal virtual async Task PublishState(BatteryState state)
    {
        await Context.MqttEntityManager.SetStateAsync(SENSOR_BATTERY_RTE, state.RoundTripEfficiency.ToString("N", new NumberFormatInfo { NumberDecimalSeparator = "." }));
    }

    public virtual void Dispose()
    {
        // TODO release managed resources here
    }
}