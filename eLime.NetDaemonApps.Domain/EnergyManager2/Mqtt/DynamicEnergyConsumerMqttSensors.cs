using eLime.NetDaemonApps.Domain.EnergyManager;
using eLime.NetDaemonApps.Domain.EnergyManager2.PersistableState;
using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Domain.Mqtt;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.HassModel;

namespace eLime.NetDaemonApps.Domain.EnergyManager2.Mqtt;

#pragma warning disable CS8618, CS9264, CS8604
public class DynamicEnergyConsumerMqttSensors : EnergyConsumerMqttSensors
{
    private readonly string SELECT_CONSUMER_BALANCING_METHOD;
    private readonly string SELECT_CONSUMER_BALANCE_ON_BEHALF_OF;
    private readonly string SELECT_CONSUMER_ALLOW_BATTERY_POWER;

    public DynamicEnergyConsumerMqttSensors(string name, IMqttEntityManager mqttEntityManager) : base(name, mqttEntityManager)
    {
        SELECT_CONSUMER_BALANCING_METHOD = $"sensor.energy_consumer_{Name.MakeHaFriendly()}_balancing_method";
        SELECT_CONSUMER_BALANCE_ON_BEHALF_OF = $"sensor.energy_consumer_{Name.MakeHaFriendly()}_balance_on_behalf_of";
        SELECT_CONSUMER_ALLOW_BATTERY_POWER = $"sensor.energy_consumer_{Name.MakeHaFriendly()}_allow_battery_power";
    }

    public event EventHandler<BalancingMethodChangedEventArgs>? BalancingMethodChangedEvent;
    private IDisposable? BalancingMethodObservable { get; set; }

    private void OnBalancingMethodChanged(BalancingMethodChangedEventArgs e)
    {
        BalancingMethodChangedEvent?.Invoke(this, e);
    }
    private Func<string, Task> BalancingMethodChangedChangedEventHandler()
    {
        return state =>
        {
            OnBalancingMethodChanged(BalancingMethodChangedEventArgs.Create(Enum<BalancingMethod>.Cast(state)));
            return Task.CompletedTask;
        };
    }

    public event EventHandler<BalanceOnBehalfOfChangedEventArgs>? BalanceOnBehalfOfChangedEvent;
    private IDisposable? BalanceOnBehalfOfObservable { get; set; }
    private void OnBalanceOnBehalfOfChanged(BalanceOnBehalfOfChangedEventArgs e)
    {
        BalanceOnBehalfOfChangedEvent?.Invoke(this, e);
    }
    private Func<string, Task> BalanceOnBehalfOfChangedChangedEventHandler()
    {
        return state =>
        {
            OnBalanceOnBehalfOfChanged(BalanceOnBehalfOfChangedEventArgs.Create(state));
            return Task.CompletedTask;
        };
    }

    public event EventHandler<AllowBatteryPowerChangedEventArgs>? AllowBatteryPowerChangedEvent;
    private IDisposable? AllowBatteryPowerObservable { get; set; }
    private void OnAllowBatteryPowerChanged(AllowBatteryPowerChangedEventArgs e)
    {
        AllowBatteryPowerChangedEvent?.Invoke(this, e);
    }
    private Func<string, Task> AllowBatteryPowerChangedChangedEventHandler()
    {
        return state =>
        {
            OnAllowBatteryPowerChanged(AllowBatteryPowerChangedEventArgs.Create(Enum<AllowBatteryPower>.Cast(state)));
            return Task.CompletedTask;
        };
    }

    internal new async Task CreateOrUpdateEntities(List<string> consumerGroups)
    {
        await base.CreateOrUpdateEntities(consumerGroups);

        var balancingMethodCreationOptions = new EntityCreationOptions(UniqueId: SELECT_CONSUMER_BALANCING_METHOD, Name: $"Dynamic load balancing method - {Name}", DeviceClass: "select", Persist: true);
        var balancingMethodDropdownOptions = new SelectOptions { Icon = "mdi:car-turbocharger", Options = Enum<BalancingMethod>.AllValuesAsStringList(), Device = Device };
        await MqttEntityManager.CreateAsync(SELECT_CONSUMER_BALANCING_METHOD, balancingMethodCreationOptions, balancingMethodDropdownOptions);

        var smartGridReadyModeObservable = await MqttEntityManager.PrepareCommandSubscriptionAsync(SELECT_CONSUMER_BALANCING_METHOD);
        BalancingMethodObservable = smartGridReadyModeObservable.SubscribeAsync(BalancingMethodChangedChangedEventHandler());

        var balanceOnBehalfOfCreationOptions = new EntityCreationOptions(UniqueId: SELECT_CONSUMER_BALANCE_ON_BEHALF_OF, Name: $"Dynamic load balance on behalf of - {Name}", DeviceClass: "select", Persist: true);
        var balanceOnBehalfOfDropdownOptions = new SelectOptions { Icon = "mdi:car-turbocharger", Options = consumerGroups, Device = Device };
        await MqttEntityManager.CreateAsync(SELECT_CONSUMER_BALANCE_ON_BEHALF_OF, balanceOnBehalfOfCreationOptions, balanceOnBehalfOfDropdownOptions);

        var balanceOnBehalfOfObservable = await MqttEntityManager.PrepareCommandSubscriptionAsync(SELECT_CONSUMER_BALANCE_ON_BEHALF_OF);
        BalanceOnBehalfOfObservable = balanceOnBehalfOfObservable.SubscribeAsync(BalanceOnBehalfOfChangedChangedEventHandler());

        var allowBatteryPowerCreationOptions = new EntityCreationOptions(UniqueId: SELECT_CONSUMER_ALLOW_BATTERY_POWER, Name: $"Dynamic load allow battery power - {Name}", DeviceClass: "select", Persist: true);
        var allowBatteryPowerDropdownOptions = new SelectOptions { Icon = "fapro:battery-bolt", Options = Enum<AllowBatteryPower>.AllValuesAsStringList(), Device = Device };
        await MqttEntityManager.CreateAsync(SELECT_CONSUMER_ALLOW_BATTERY_POWER, allowBatteryPowerCreationOptions, allowBatteryPowerDropdownOptions);

        var allowBatteryPowerObservable = await MqttEntityManager.PrepareCommandSubscriptionAsync(SELECT_CONSUMER_ALLOW_BATTERY_POWER);
        AllowBatteryPowerObservable = allowBatteryPowerObservable.SubscribeAsync(AllowBatteryPowerChangedChangedEventHandler());
    }

    internal new async Task PublishState(ConsumerState state)
    {
        await base.PublishState(state);
        await MqttEntityManager.SetStateAsync(SELECT_CONSUMER_BALANCING_METHOD, state.BalancingMethod.ToString());
        await MqttEntityManager.SetStateAsync(SELECT_CONSUMER_BALANCE_ON_BEHALF_OF, state.BalanceOnBehalfOf);
        await MqttEntityManager.SetStateAsync(SELECT_CONSUMER_ALLOW_BATTERY_POWER, state.AllowBatteryPower.ToString());
    }

    public new void Dispose()
    {
        base.Dispose();
        BalancingMethodObservable?.Dispose();
        BalanceOnBehalfOfObservable?.Dispose();
        AllowBatteryPowerObservable?.Dispose();
    }
}