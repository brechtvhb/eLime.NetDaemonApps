﻿using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
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

        private SmartWasherState? _state;

        public DateTimeOffset? LastStateChange;
        public DateTimeOffset? Eta { get; set; }
        public WasherProgram? Program { get; set; }

        private IDisposable SwitchDisposable { get; set; }
        private IDisposable DelayedStartDisposable { get; set; }
        private IDisposable DelayedStartTriggerDisposable { get; set; }
        private IDisposable? NoPowerResetter { get; set; }

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
            PowerSensor.Changed += PowerSensor_Changed;
            PowerSocket = powerSocket;
            PowerSocket.TurnedOn += PowerSocket_TurnedOn;
            PowerSocket.TurnedOff += PowerSocket_TurnedOff;

            EnsureSensorsExist().RunSync();
            var state = RetrieveSateFromHomeAssistant();

            SmartWasherState initState = state switch
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

            TransitionTo(_logger, initState);
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
                await _mqttEntityManager.SetStateAsync(switchName, "ON");
            }

            var switchObserver = await _mqttEntityManager.PrepareCommandSubscriptionAsync(switchName);
            SwitchDisposable = switchObserver.SubscribeAsync(EnabledSwitchHandler(switchName));

            var delayedStartObserver = await _mqttEntityManager.PrepareCommandSubscriptionAsync(delayedStartName);
            DelayedStartDisposable = delayedStartObserver.SubscribeAsync(DelayedStartSwitchHandler(delayedStartName));

            var delayedStartTriggerObserver = await _mqttEntityManager.PrepareCommandSubscriptionAsync(delayedStartTriggerName);
            DelayedStartTriggerDisposable = delayedStartTriggerObserver.SubscribeAsync(DelayedStartTriggerHandler(delayedStartTriggerName));
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
                LasStateChange = LastStateChange?.ToString("O"),
                Eta = Eta?.ToString("O"),
                WasherState = State.ToString(),
                Program = Program?.ToString(),
                Icon = "mdi:washing-machine"
            };
            await _mqttEntityManager.SetAttributesAsync(EnabledSwitch.EntityId, attributes);
            await _mqttEntityManager.SetStateAsync($"sensor.smartwasher_{Name.MakeHaFriendly()}_state", State.ToString());
            _logger.LogTrace("{SmartWasher}: Updated smartwasher state in Home assistant to {Attributes}", Name, attributes);
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

            if (e.New?.State == 0 && NoPowerResetter == null && State != WasherStates.Idle && State != WasherStates.DelayedStart && State != WasherStates.Ready)
            {
                _logger.LogInformation("Wonky washer detected! Will reset washer to Idle in 5 minutes if socket power usage remains 0.");
                NoPowerResetter = _scheduler.Schedule(TimeSpan.FromMinutes(5), (_, _) => TransitionTo(_logger, new IdleState()));
            }

            if (e.New?.State != 0 && NoPowerResetter != null)
                NoPowerResetter.Dispose();

            _state?.PowerUsageChanged(_logger, _scheduler, this);
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
            if (_state != null)
                logger.LogDebug($"{{SmartWasher}}: Transitioning from state {_state.GetType().Name.Replace("State", "")} to {state.GetType().Name.Replace("State", "")}", Name);
            else
                logger.LogDebug($"{{SmartWasher}}: Initialized in {state.GetType().Name.Replace("State", "")} state.", Name);

            LastStateChange = _scheduler.Now;
            _state = state;
            _state.Enter(logger, _scheduler, this);
            Eta = _state.GetEta(_logger, this);

            UpdateStateInHomeAssistant().RunSync();
        }

        internal void SetWasherProgram(ILogger logger, WasherProgram? program)
        {
            Program = program;
            Eta = _state.GetEta(_logger, this);

            UpdateStateInHomeAssistant().RunSync();
            logger.LogInformation($"{{SmartWasher}}: Set washer program to {program}.", Name);
        }

        public void Dispose()
        {
            _logger.LogInformation($"{{SmartWasher}}: Disposing.", Name);
            PowerSensor.Changed -= PowerSensor_Changed;
            PowerSocket.TurnedOn -= PowerSocket_TurnedOn;
            PowerSocket.TurnedOff -= PowerSocket_TurnedOff;

            SwitchDisposable.Dispose();
            DelayedStartDisposable.Dispose();
            DelayedStartTriggerDisposable.Dispose();
            PowerSensor.Dispose();
            PowerSocket.Dispose();

            NoPowerResetter?.Dispose();
            _logger.LogInformation($"{{SmartWasher}}: Disposed.", Name);
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
