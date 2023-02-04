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
    public class SmartWasher : IDisposable
    {
        public string? Name { get; }
        private SmartWasherSwitch EnabledSwitch { get; set; }
        private SmartWasherDelayedStartSwitch DelayedStart { get; set; }
        private SmartWasherDelayedStartTrigger DelayedStartTrigger { get; set; }

        public NumericSensor PowerSensor { get; set; }
        public BinarySwitch PowerSocket { get; set; }

        private readonly IHaContext _haContext;
        private readonly ILogger _logger;
        private readonly IScheduler _scheduler;
        private readonly IMqttEntityManager _mqttEntityManager;

        private SmartWasherState _state;

        public DateTimeOffset LastStateChange;
        public DateTimeOffset? Eta { get; set; }
        public WasherProgram? Program { get; set; }

        public WasherStates State => _state switch
        {
            IdleState => WasherStates.Idle,
            DelayedStartState => WasherStates.DelayedStart,
            PreWashingState => WasherStates.PreWashing,
            HeatingState => WasherStates.Heating,
            WashingState => WasherStates.Washing,
            RinsingState => WasherStates.Rinsing,
            SpinningState => WasherStates.Spinning,
            ReadyState => WasherStates.Ready,
            _ => WasherStates.Idle
        };

        public SmartWasher(ILogger logger, IHaContext haContext, IMqttEntityManager mqttEntityManager, IScheduler scheduler, bool enabled, string name, BinarySwitch powerSocket, NumericSensor powerSensor)
        {
            _logger = logger;
            _haContext = haContext;
            _mqttEntityManager = mqttEntityManager;
            _scheduler = scheduler;

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
                WasherStates.DelayedStart => new DelayedStartState(),
                WasherStates.PreWashing => new PreWashingState(),
                WasherStates.Heating => new HeatingState(),
                WasherStates.Washing => new WashingState(),
                WasherStates.Rinsing => new RinsingState(),
                WasherStates.Spinning => new SpinningState(),
                WasherStates.Ready => new ReadyState(),
                _ => new IdleState(),
            };

            _state.Enter(_logger, _scheduler, this);

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
                await _mqttEntityManager.CreateAsync(stateName, new EntityCreationOptions(Name: $"Smart washer - {Name} - state", UniqueId: $"smartwasher_{Name}_state", Persist: true));
                await _mqttEntityManager.SetStateAsync(stateName, State.ToString());
                created = true;
            }

            EnabledSwitch = new SmartWasherSwitch(_haContext, switchName);
            DelayedStart = new SmartWasherDelayedStartSwitch(_haContext, delayedStartName);
            DelayedStartTrigger = new SmartWasherDelayedStartTrigger(_haContext, delayedStartTriggerName);
            DelayedStartTrigger.Initialize();
            DelayedStartTrigger.TurnedOn += DelayedStartTrigger_TurnedOn;
            if (created)
            {
                _mqttEntityManager.SetStateAsync(switchName, "ON").RunSync();
                UpdateStateInHomeAssistant().RunSync();
            }

            await UpdateStateInHomeAssistant();

            var switchObserver = await _mqttEntityManager.PrepareCommandSubscriptionAsync(switchName);
            switchObserver.SubscribeAsync(EnabledSwitchHandler(switchName));

            var delayedStartObserver = await _mqttEntityManager.PrepareCommandSubscriptionAsync(delayedStartName);
            delayedStartObserver.SubscribeAsync(DelayedStartSwitchHandler(delayedStartName));

            var delayedStartTriggerObserver = await _mqttEntityManager.PrepareCommandSubscriptionAsync(delayedStartTriggerName);
            delayedStartTriggerObserver.SubscribeAsync(DelayedStartTriggerHandler(delayedStartTriggerName));
        }

        private void DelayedStartTrigger_TurnedOn(object? sender, EnabledSwitchEventArgs<EnabledSwitchAttributes> e)
        {
            if (!DelayedStart.IsOn() || _state is not DelayedStartState)
                return;

            TurnPowerSocketOn();
            _logger.LogDebug("{SmartWasher}: Awakening from delayed start.", Name);
        }

        private Func<string, Task> DelayedStartTriggerHandler(string delayedStartTriggerName)
        {
            return async state =>
            {
                _logger.LogDebug("{SmartWasher}: Setting delayed start trigger to {state}.", Name, state);
                await _mqttEntityManager.SetStateAsync(delayedStartTriggerName, state);
            };
        }

        private Func<string, Task> DelayedStartSwitchHandler(string delayedStartName)
        {
            return async state =>
            {
                _logger.LogDebug("{SmartWasher}: Setting delayed start to {state}.", Name, state);
                await _mqttEntityManager.SetStateAsync(delayedStartName, state);
            };
        }

        private Func<string, Task> EnabledSwitchHandler(string switchName)
        {
            return async state =>
            {
                _logger.LogDebug("{SmartWasher}: Setting smart washer switch state to {state}.", Name, state);
                if (state == "OFF")
                {
                    _logger.LogDebug("{SmartWasher}: Clearing smart washer switch state because it was disabled.", Name);
                    await UpdateStateInHomeAssistant();
                }
                await _mqttEntityManager.SetStateAsync(switchName, state);
            };
        }

        private async Task UpdateStateInHomeAssistant()
        {
            if (!IsEnabled())
                return;

            var attributes = new SmartWasherSwitchAttributes
            {
                LastUpdated = DateTime.Now.ToString("O"),
                Eta = Eta?.ToString("O"),
                WasherState = State.ToString(),
                Program = Program?.ToString(),
                Icon = "mdi:washing-machine"
            };
            await _mqttEntityManager.SetAttributesAsync(EnabledSwitch.EntityId, attributes);
            await _mqttEntityManager.SetStateAsync($"sensor.smartwasher_{Name.MakeHaFriendly()}_state", State.ToString());
            _logger.LogTrace("{SmartWasher}: Updated flexiscreen state in Home assistant to {Attributes}", Name, attributes);
        }

        private WasherStates? RetrieveSateFromHomeAssistant()
        {
            Eta = !string.IsNullOrWhiteSpace(EnabledSwitch.Attributes?.Eta) ? DateTime.Parse(EnabledSwitch.Attributes.Eta) : null;
            WasherStates? state = !string.IsNullOrWhiteSpace(EnabledSwitch.Attributes?.WasherState) ? Enum<WasherStates>.Cast(EnabledSwitch.Attributes.WasherState) : null;
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
            if (!IsEnabled())
                return;

            _state.PowerUsageChanged(_logger, _scheduler, this);
        }

        private void PowerSocket_TurnedOn(object? sender, BinarySensorEventArgs e)
        {
            if (!IsEnabled())
                return;

            if (_state is not DelayedStartState delayedStartState)
                return;

            delayedStartState.Start(_logger, this);
        }

        private void PowerSocket_TurnedOff(object? sender, BinarySensorEventArgs e)
        {
            return;
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
            LastStateChange = _scheduler.Now;
            _state = state;
            _state.Enter(logger, _scheduler, this);
            Eta = _state.GetEta(_logger, this);

            UpdateStateInHomeAssistant().RunSync();
        }

        internal void SetWasherProgram(WasherProgram? program)
        {
            Program = program;
            Eta = _state.GetEta(_logger, this);

            UpdateStateInHomeAssistant().RunSync();
        }

        public void Dispose()
        {
            PowerSensor.Changed -= PowerSensor_Changed;
            PowerSocket.TurnedOn -= PowerSocket_TurnedOn;
            PowerSocket.TurnedOff -= PowerSocket_TurnedOff;

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
