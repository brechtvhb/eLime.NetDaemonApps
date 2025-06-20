using eLime.NetDaemonApps.Domain.EnergyManager.Consumers;
using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Domain.Mqtt;
using NetDaemon.Extensions.MqttEntityManager;

namespace eLime.NetDaemonApps.Domain.EnergyManager;

#pragma warning disable CS8618, CS9264, CS8604
public class EnergyManagerMqttSensors(EnergyManagerContext context)
{
    private static Device GetDevice() => new() { Identifiers = ["energy_manager"], Name = "Energy manager", Manufacturer = "Me" };
    private readonly string SENSOR_STATE = "sensor.energy_manager_state";

    internal async Task CreateOrUpdateEntities()
    {
        var sensorStateCreationOptions = new EntityCreationOptions(DeviceClass: "enum", UniqueId: SENSOR_STATE, Name: "Energy manager state", Persist: true);
        var sensorStateOptions = new EnumSensorOptions { Icon = "fapro:square-bolt", Device = GetDevice(), Options = Enum<EnergyConsumerState>.AllValuesAsStringList() };
        await context.MqttEntityManager.CreateAsync(SENSOR_STATE, sensorStateCreationOptions, sensorStateOptions);
    }

    internal async Task PublishState(EnergyManagerState state)
    {
        var globalAttributes = new EnergyManagerAttributes
        {
            RunningConsumers = state.RunningConsumers,
            NeedEnergyConsumers = state.NeedEnergyConsumers,
            CriticalNeedEnergyConsumers = state.CriticalNeedEnergyConsumers,
            LastUpdated = state.LastChange.ToString("O"),
        };

        await context.MqttEntityManager.SetStateAsync(SENSOR_STATE, state.State.ToString());
        await context.MqttEntityManager.SetAttributesAsync(SENSOR_STATE, globalAttributes);
    }

}