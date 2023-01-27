using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.FlexiScreens;
using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Domain.SmartWashers.States;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.HassModel;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SmartWashers
{
    public class SmartWasher
    {
        public string? Name { get; }
        private SmartWasherSwitch EnabledSwitch { get; set; }
        private SmartWasherDelayedStartSwitch DelayedStart { get; set; }
        private SmartWasherDelayedStartSwitch DelayedStartTrigger { get; set; }

        public NumericSensor PowerSensor { get; set; }
        public BinarySwitch PowerSocket { get; set; }

        private readonly IHaContext _haContext;
        private readonly ILogger _logger;
        private readonly IScheduler _scheduler;
        private readonly IMqttEntityManager _mqttEntityManager;

        private SmartWasherState _state;

        public DateTime LastStateChange;
        public DateTime? Eta { get; set; }
        public WasherProgram? Program { get; set; }

        public WasherStates State => _state switch
        {
            IdleState => WasherStates.Idle,
            PreWashingState => WasherStates.PreWashing,
            HeatingState => WasherStates.Heating,
            _ => throw new ArgumentOutOfRangeException(nameof(_state))
        };

        //TODO: create brol
        public SmartWasher(ILogger logger, IMqttEntityManager mqttEntityManager, bool enabled, string name, BinarySwitch powerSocket, NumericSensor powerSensor)
        {
            _logger = logger;
            _mqttEntityManager = mqttEntityManager;

            if (!enabled)
                return;

            Name = name;
            PowerSensor = powerSensor;
            PowerSocket = powerSocket;

            EnsureSensorsExist().RunSync();
            var state = RetrieveSateFromHomeAssistant();

            _state = state switch
            {
                WasherStates.Idle => new IdleState(),
                WasherStates.DelayedStart => throw new NotImplementedException(),
                WasherStates.PreWashing => new PreWashingState(),
                WasherStates.Heating => new HeatingState(),
                WasherStates.Washing => new WashingState(),
                WasherStates.Rinsing => new RinsingState(),
                WasherStates.Spinning => new SpinningState(),
                WasherStates.Ready => new ReadyState(),
                _ => new IdleState(),
            };

            _state.Enter(_logger, this);

            PowerSensor.Changed += PowerSensor_Changed;
            PowerSocket.TurnedOn += PowerSocket_TurnedOn;
            PowerSocket.TurnedOff += PowerSocket_TurnedOff;
        }

        private async Task EnsureSensorsExist()
        {
            var switchName = $"switch.smartwasher_{Name.MakeHaFriendly()}";
            var delayedStartName = $"switch.smartwasher_{Name.MakeHaFriendly()}_delayed_start";
            var delayedStartTriggerName = $"switch.smartwasher_{Name.MakeHaFriendly()}_delayed_start_activate";
            var stateName = $"sensor.smartwasher_{Name.MakeHaFriendly()}_state";

            var created = false;
            if (_haContext.Entity(switchName).State == null || string.Equals(_haContext.Entity(switchName).State, "unavailable", StringComparison.InvariantCultureIgnoreCase))
            {
                _logger.LogDebug("{SmartWasher}: Creating Enabled switch in home assistant.", Name);
                await _mqttEntityManager.CreateAsync(switchName, new EntityCreationOptions(Name: $"Smart washer - {Name}", DeviceClass: "switch", Persist: true));
                await _mqttEntityManager.CreateAsync(delayedStartName, new EntityCreationOptions(Name: $"Smart washer - {Name} - Delayed start", DeviceClass: "switch", Persist: true));
                await _mqttEntityManager.CreateAsync(delayedStartTriggerName, new EntityCreationOptions(Name: $"Smart washer - {Name} - Delayed start - Activate", DeviceClass: "switch", Persist: true));
                await _mqttEntityManager.CreateAsync(delayedStartName, new EntityCreationOptions(Name: $"Smart washer - {Name} - state", DeviceClass: "sensor", Persist: true));
                created = true;
            }

            EnabledSwitch = new SmartWasherSwitch(_haContext, switchName);

            if (created)
            {
                _mqttEntityManager.SetStateAsync(switchName, "ON").RunSync();
                UpdateStateInHomeAssistant().RunSync();
            }

            var switchObserver = await _mqttEntityManager.PrepareCommandSubscriptionAsync(switchName);

            switchObserver
                .SubscribeAsync(async state =>
                {
                    _logger.LogDebug("{SmartWasher}: Setting smart washer switch state to {state}.", Name, state);
                    if (state == "OFF")
                    {
                        _logger.LogDebug("{SmartWasher}: Clearing smart washer switch state because it was disabled.", Name);
                        await UpdateStateInHomeAssistant();
                    }
                    await _mqttEntityManager.SetStateAsync(switchName, state);
                });

            var delayedStartObserver = await _mqttEntityManager.PrepareCommandSubscriptionAsync(delayedStartName);

            delayedStartObserver
                .SubscribeAsync(async state =>
                {
                    _logger.LogDebug("{SmartWasher}: Setting delayed start to {state}.", Name, state);
                    await _mqttEntityManager.SetStateAsync(switchName, delayedStartName);
                });

            var delayedStartTriggerObserver = await _mqttEntityManager.PrepareCommandSubscriptionAsync(delayedStartName);

            delayedStartTriggerObserver
                .SubscribeAsync(async state =>
                {
                    _logger.LogDebug("{SmartWasher}: Setting delayed start trigger to {state}.", Name, state);
                    await _mqttEntityManager.SetStateAsync(switchName, delayedStartName);

                    if (DelayedStart.IsOn() && DelayedStartTrigger.IsOn() && _state is DelayedStartState delayedStartState)
                    {
                        TurnPowerSocketOn();
                    }
                });
        }

        private async Task UpdateStateInHomeAssistant()
        {
            if (!IsEnabled())
                return;

            var attributes = new SmartWasherSwitchAttributes
            {
                LastUpdated = DateTime.Now.ToString("O"),
                Eta = Eta?.ToString("O"),
                State = State.ToString(),
                Program = Program.ToString(),
                Icon = "mdi:washing-machine"
            };
            await _mqttEntityManager.SetAttributesAsync(EnabledSwitch.EntityId, attributes);
            await _mqttEntityManager.SetStateAsync($"sensor.smartwasher_{Name.MakeHaFriendly()}_state", State.ToString());
            _logger.LogTrace("{SmartWasher}: Updated flexiscreen state in Home assistant to {Attributes}", Name, attributes);
        }

        private WasherStates? RetrieveSateFromHomeAssistant()
        {
            Eta = !string.IsNullOrWhiteSpace(EnabledSwitch.Attributes?.Eta) ? DateTime.Parse(EnabledSwitch.Attributes.Eta) : null;
            WasherStates? state = !string.IsNullOrWhiteSpace(EnabledSwitch.Attributes?.State) ? Enum<WasherStates>.Cast(EnabledSwitch.Attributes.State) : null;
            Program = !string.IsNullOrWhiteSpace(EnabledSwitch.Attributes?.Program) ? Enum<WasherProgram>.Cast(EnabledSwitch.Attributes.Program) : null;
            _logger.LogDebug("{SmartWasher}: Retrieved smart washer state state from Home assistant'.", Name);

            return state;
        }

        private bool IsEnabled()
        {
            return EnabledSwitch.IsOn();
        }

        internal bool IsDelayedStartEnabled()
        {
            return DelayedStart.IsOn();
        }

        private void PowerSensor_Changed(object? sender, NumericSensorEventArgs e)
        {
            _state.PowerUsageChanged(_logger, this);
        }

        private void PowerSocket_TurnedOn(object? sender, BinarySensorEventArgs e)
        {
            if (_state is not DelayedStartState delayedStartState)
                return;

            delayedStartState.Start(_logger, this);
        }

        private void PowerSocket_TurnedOff(object? sender, BinarySensorEventArgs e)
        {
            _state.PowerUsageChanged(_logger, this);
        }

        internal void TurnPowerSocketOFf()
        {
            PowerSocket.TurnOff();
        }

        internal void TurnPowerSocketOn()
        {
            PowerSocket.TurnOn();
        }

        internal void TransitionTo(ILogger logger, SmartWasherState state)
        {
            logger.LogDebug($"SmartWasher: Transitioning from state {_state.GetType().Name} to {state.GetType().Name}");
            LastStateChange = DateTime.Now;
            _state = state;
            _state.Enter(logger, this);
            Eta = _state.GetEta(_logger, this);

            UpdateStateInHomeAssistant().RunSync();
        }

        internal void SetWasherProgram(WasherProgram? program)
        {
            Program = program;
            Eta = _state.GetEta(_logger, this);

            UpdateStateInHomeAssistant().RunSync();
        }
    }

    public enum WasherStates
    {
        Idle,
        DelayedStart,
        PreWashing,
        Heating,
        Washing,
        Rinsing,
        Spinning,
        Ready
    }

    public enum WasherProgram
    {
        Unknown,
        Wash40Degrees,
        Wash60Degrees,
    }
}
