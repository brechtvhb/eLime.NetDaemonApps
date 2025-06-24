using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Domain.Mqtt;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.HassModel;

namespace eLime.NetDaemonApps.Domain.EnergyManager.Consumers.DynamicConsumers;

#pragma warning disable CS8618, CS9264, CS8604
public class DynamicEnergyConsumerMqttSensors : EnergyConsumerMqttSensors
{
    private readonly string SELECT_CONSUMER_BALANCING_METHOD;
    private readonly string SELECT_CONSUMER_BALANCE_ON_BEHALF_OF;
    private readonly string SELECT_CONSUMER_ALLOW_BATTERY_POWER;

    public DynamicEnergyConsumerMqttSensors(string name, EnergyManagerContext context) : base(name, context)
    {
        SELECT_CONSUMER_BALANCING_METHOD = $"select.energy_consumer_{Name.MakeHaFriendly()}_balancing_method";
        SELECT_CONSUMER_BALANCE_ON_BEHALF_OF = $"select.energy_consumer_{Name.MakeHaFriendly()}_balance_on_behalf_of";
        SELECT_CONSUMER_ALLOW_BATTERY_POWER = $"select.energy_consumer_{Name.MakeHaFriendly()}_allow_battery_power";
    }

    public event EventHandler<BalancingMethodChangedEventArgs>? BalancingMethodChanged;
    private IDisposable? BalancingMethodObservable { get; set; }

    private void OnBalancingMethodChanged(BalancingMethodChangedEventArgs e)
    {
        BalancingMethodChanged?.Invoke(this, e);
    }
    private Func<string, Task> BalancingMethodChangedChangedEventHandler()
    {
        return state =>
        {
            OnBalancingMethodChanged(BalancingMethodChangedEventArgs.Create(Enum<BalancingMethod>.Cast(state)));
            return Task.CompletedTask;
        };
    }

    public event EventHandler<BalanceOnBehalfOfChangedEventArgs>? BalanceOnBehalfOfChanged;
    private IDisposable? BalanceOnBehalfOfObservable { get; set; }
    private void OnBalanceOnBehalfOfChanged(BalanceOnBehalfOfChangedEventArgs e)
    {
        BalanceOnBehalfOfChanged?.Invoke(this, e);
    }
    private Func<string, Task> BalanceOnBehalfOfChangedChangedEventHandler()
    {
        return state =>
        {
            OnBalanceOnBehalfOfChanged(BalanceOnBehalfOfChangedEventArgs.Create(state));
            return Task.CompletedTask;
        };
    }

    public event EventHandler<AllowBatteryPowerChangedEventArgs>? AllowBatteryPowerChanged;
    private IDisposable? AllowBatteryPowerObservable { get; set; }
    private void OnAllowBatteryPowerChanged(AllowBatteryPowerChangedEventArgs e)
    {
        AllowBatteryPowerChanged?.Invoke(this, e);
    }
    private Func<string, Task> AllowBatteryPowerChangedChangedEventHandler()
    {
        return state =>
        {
            OnAllowBatteryPowerChanged(AllowBatteryPowerChangedEventArgs.Create(Enum<AllowBatteryPower>.Cast(state)));
            return Task.CompletedTask;
        };
    }

    internal override async Task CreateOrUpdateEntities(List<string> consumerGroups)
    {
        await base.CreateOrUpdateEntities(consumerGroups);

        var balancingMethodCreationOptions = new EntityCreationOptions(UniqueId: SELECT_CONSUMER_BALANCING_METHOD, Name: $"Dynamic load balancing method - {Name}", DeviceClass: "select", Persist: true);
        var balancingMethodDropdownOptions = new SelectOptions { Icon = "mdi:car-turbocharger", Options = Enum<BalancingMethod>.AllValuesAsStringList(), Device = Device };
        await Context.MqttEntityManager.CreateAsync(SELECT_CONSUMER_BALANCING_METHOD, balancingMethodCreationOptions, balancingMethodDropdownOptions);

        var balancingMethodObservable = await Context.MqttEntityManager.PrepareCommandSubscriptionAsync(SELECT_CONSUMER_BALANCING_METHOD);
        BalancingMethodObservable = balancingMethodObservable.SubscribeAsync(BalancingMethodChangedChangedEventHandler());

        var balanceOnBehalfOfCreationOptions = new EntityCreationOptions(UniqueId: SELECT_CONSUMER_BALANCE_ON_BEHALF_OF, Name: $"Dynamic load balance on behalf of - {Name}", DeviceClass: "select", Persist: true);
        var balanceOnBehalfOfDropdownOptions = new SelectOptions { Icon = "mdi:car-turbocharger", Options = consumerGroups, Device = Device };
        await Context.MqttEntityManager.CreateAsync(SELECT_CONSUMER_BALANCE_ON_BEHALF_OF, balanceOnBehalfOfCreationOptions, balanceOnBehalfOfDropdownOptions);

        var balanceOnBehalfOfObservable = await Context.MqttEntityManager.PrepareCommandSubscriptionAsync(SELECT_CONSUMER_BALANCE_ON_BEHALF_OF);
        BalanceOnBehalfOfObservable = balanceOnBehalfOfObservable.SubscribeAsync(BalanceOnBehalfOfChangedChangedEventHandler());

        var allowBatteryPowerCreationOptions = new EntityCreationOptions(UniqueId: SELECT_CONSUMER_ALLOW_BATTERY_POWER, Name: $"Dynamic load allow battery power - {Name}", DeviceClass: "select", Persist: true);
        var allowBatteryPowerDropdownOptions = new SelectOptions { Icon = "fapro:battery-bolt", Options = Enum<AllowBatteryPower>.AllValuesAsStringList(), Device = Device };
        await Context.MqttEntityManager.CreateAsync(SELECT_CONSUMER_ALLOW_BATTERY_POWER, allowBatteryPowerCreationOptions, allowBatteryPowerDropdownOptions);

        var allowBatteryPowerObservable = await Context.MqttEntityManager.PrepareCommandSubscriptionAsync(SELECT_CONSUMER_ALLOW_BATTERY_POWER);
        AllowBatteryPowerObservable = allowBatteryPowerObservable.SubscribeAsync(AllowBatteryPowerChangedChangedEventHandler());
    }

    internal override async Task PublishState(ConsumerState state)
    {
        await base.PublishState(state);
        await Context.MqttEntityManager.SetStateAsync(SELECT_CONSUMER_BALANCING_METHOD, state.BalancingMethod.ToString());
        await Context.MqttEntityManager.SetStateAsync(SELECT_CONSUMER_BALANCE_ON_BEHALF_OF, state.BalanceOnBehalfOf);
        await Context.MqttEntityManager.SetStateAsync(SELECT_CONSUMER_ALLOW_BATTERY_POWER, state.AllowBatteryPower.ToString());
    }

    public override void Dispose()
    {
        base.Dispose();
        BalancingMethodObservable?.Dispose();
        BalanceOnBehalfOfObservable?.Dispose();
        AllowBatteryPowerObservable?.Dispose();
    }
}