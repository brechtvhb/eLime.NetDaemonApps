using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Domain.Mqtt;
using NetDaemon.Extensions.MqttEntityManager;

namespace eLime.NetDaemonApps.Domain.EnergyManager.Consumers;

#pragma warning disable CS8618, CS9264, CS8604
public class EnergyConsumerMqttSensors : IDisposable
{
    private readonly string SENSOR_CONSUMER_STATE;
    private readonly string SENSOR_CONSUMER_STARTED_AT;
    private readonly string SENSOR_CONSUMER_LAST_RUN;

    protected Device Device;
    protected readonly EnergyManagerContext Context;
    internal readonly string Name;

    public EnergyConsumerMqttSensors(string name, EnergyManagerContext context)
    {
        Context = context;

        Name = name;
        Device = new Device { Identifiers = [$"energy_consumer_{Name.MakeHaFriendly()}"], Name = "Energy consumer: " + Name, Manufacturer = "Me" };
        SENSOR_CONSUMER_STATE = $"sensor.energy_consumer_{Name.MakeHaFriendly()}_state";
        SENSOR_CONSUMER_STARTED_AT = $"sensor.energy_consumer_{Name.MakeHaFriendly()}_started_at";
        SENSOR_CONSUMER_LAST_RUN = $"sensor.energy_consumer_{Name.MakeHaFriendly()}_last_run";
    }

    internal virtual async Task CreateOrUpdateEntities(List<string> consumerGroups)
    {
        var stateCreationOptions = new EntityCreationOptions(DeviceClass: "enum", UniqueId: SENSOR_CONSUMER_STATE, Name: $"Consumer {Name} - state", Persist: true);
        var stateOptions = new EnumSensorOptions { Icon = "fapro:square-bolt", Device = Device, Options = Enum<EnergyConsumerState>.AllValuesAsStringList() };
        await Context.MqttEntityManager.CreateAsync(SENSOR_CONSUMER_STATE, stateCreationOptions, stateOptions);

        var startedAtCreationOptions = new EntityCreationOptions(UniqueId: SENSOR_CONSUMER_STARTED_AT, Name: $"Consumer {Name} - Started at", DeviceClass: "timestamp", Persist: true);
        var startedAtOptions = new EntityOptions { Icon = "mdi:calendar-start-outline", Device = Device };
        await Context.MqttEntityManager.CreateAsync(SENSOR_CONSUMER_STARTED_AT, startedAtCreationOptions, startedAtOptions);

        var lastRunCreationOptions = new EntityCreationOptions(UniqueId: SENSOR_CONSUMER_LAST_RUN, Name: $"Consumer {Name} - Last run", DeviceClass: "timestamp", Persist: true);
        var lastRunOptions = new EntityOptions { Icon = "fapro:calendar-day", Device = Device };
        await Context.MqttEntityManager.CreateAsync(SENSOR_CONSUMER_LAST_RUN, lastRunCreationOptions, lastRunOptions);
    }

    internal virtual async Task PublishState(ConsumerState state)
    {
        await Context.MqttEntityManager.SetStateAsync(SENSOR_CONSUMER_STATE, state.State.ToString());
        await Context.MqttEntityManager.SetStateAsync(SENSOR_CONSUMER_STARTED_AT, state.StartedAt?.ToString("O") ?? "None");
        await Context.MqttEntityManager.SetStateAsync(SENSOR_CONSUMER_LAST_RUN, state.LastRun?.ToString("O") ?? "None");
    }

    public virtual void Dispose()
    {
        // TODO release managed resources here
    }
}