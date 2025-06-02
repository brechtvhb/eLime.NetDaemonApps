using eLime.NetDaemonApps.Domain.EnergyManager;
using eLime.NetDaemonApps.Domain.EnergyManager2.PersistableState;
using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Domain.Mqtt;
using NetDaemon.Extensions.MqttEntityManager;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.EnergyManager2.Mqtt;

#pragma warning disable CS8618, CS9264, CS8604
public class EnergyManagerMqttSensors(IScheduler scheduler, IMqttEntityManager mqttEntityManager)
{
    private static Device GetDevice() => new() { Identifiers = ["energy_manager"], Name = "Energy manager", Manufacturer = "Me" };
    private readonly string SENSOR_STATE = "sensor.energy_manager_state";

    internal async Task CreateOrUpdateEntities()
    {
        var sensorStateCreationOptions = new EntityCreationOptions(DeviceClass: "enum", UniqueId: SENSOR_STATE, Name: "Energy manager state", Persist: true);
        var sensorStateOptions = new EnumSensorOptions { Icon = "fapro:square-bolt", Device = GetDevice(), Options = Enum<EnergyConsumerState>.AllValuesAsStringList() };
        await mqttEntityManager.CreateAsync(SENSOR_STATE, sensorStateCreationOptions, sensorStateOptions);
    }

    internal async Task PublishState(EnergyManagerState state)
    {
        var globalAttributes = new EnergyManagerAttributes
        {
            LastUpdated = scheduler.Now.ToString("O"),
        };

        await mqttEntityManager.SetStateAsync(SENSOR_STATE, state.State.ToString());
        await mqttEntityManager.SetAttributesAsync(SENSOR_STATE, globalAttributes);
    }

}