using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Domain.Mqtt;
using eLime.NetDaemonApps.Domain.SmartWashers.States;
using eLime.NetDaemonApps.Domain.Storage;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.HassModel;
using System.Reactive.Concurrency;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("eLime.NetDaemonApps.Tests")]
namespace eLime.NetDaemonApps.Domain.SmartWashers
{
    public class SmartWasher : IDisposable
    {
        public string? Name { get; }

        public Boolean Enabled { get; private set; }
        public Boolean CanDelayStart { get; private set; }
        public Boolean DelayedStartTriggered { get; private set; }

        public NumericSensor PowerSensor { get; set; }
        public BinarySwitch PowerSocket { get; set; }

        private readonly IHaContext _haContext;
        private readonly ILogger _logger;
        private readonly IScheduler _scheduler;
        private readonly IFileStorage _fileStorage;
        private readonly IMqttEntityManager _mqttEntityManager;

        private SmartWasherState? _state;

        public DateTimeOffset? LastStateChange;
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? Eta { get; set; }
        public Int32? PercentageComplete { get; set; }
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

        public SmartWasher(ILogger logger, IHaContext haContext, IMqttEntityManager mqttEntityManager, IScheduler scheduler, IFileStorage fileStorage, bool enabled, string name, BinarySwitch powerSocket, NumericSensor powerSensor)
        {
            _logger = logger;
            _haContext = haContext;
            _mqttEntityManager = mqttEntityManager;
            _scheduler = scheduler;
            _fileStorage = fileStorage;

            if (!enabled)
                return;

            Name = name;
            PowerSensor = powerSensor;
            PowerSensor.Changed += PowerSensor_Changed;
            PowerSocket = powerSocket;
            PowerSocket.TurnedOn += PowerSocket_TurnedOn;
            PowerSocket.TurnedOff += PowerSocket_TurnedOff;

            EnsureSensorsExist().RunSync();
            var state = RetrieveState();

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
            var baseName = $"sensor.smartwasher_{Name.MakeHaFriendly()}";

            if (_haContext.Entity(switchName).State == null)
            {
                _logger.LogDebug("{SmartWasher}: Creating Sensors and switches in home assistant.", Name);

                var enabledOptions = new EntityOptions { Icon = "mdi:washing-machine", Device = GetDevice() };
                await _mqttEntityManager.CreateAsync(switchName, new EntityCreationOptions(Name: $"{Name}", DeviceClass: "switch", Persist: true), enabledOptions);
                Enabled = true;

                var delayedStartOptions = new EntityOptions { Icon = "mdi:timer-pause-outline", Device = GetDevice() };
                await _mqttEntityManager.CreateAsync(delayedStartName, new EntityCreationOptions(Name: $"{Name} - Delayed start", DeviceClass: "switch", Persist: true), delayedStartOptions);

                var delayedStartTriggerOptions = new EntityOptions { Icon = "mdi:timer-play-outline", Device = GetDevice() };
                await _mqttEntityManager.CreateAsync(delayedStartTriggerName, new EntityCreationOptions(Name: $"{Name} - Delayed start - Activate", DeviceClass: "switch", Persist: true), delayedStartTriggerOptions);

                var stateOptions = new EntityOptions { Icon = "mdi:progress-helper", Device = GetDevice() };
                await _mqttEntityManager.CreateAsync($"{baseName}_state", new EntityCreationOptions(Name: $"{Name} - state", UniqueId: $"smartwasher_{Name}_state", Persist: true), stateOptions);

                var startedAtOptions = new EntityOptions { Icon = "fapro:calendar-day", Device = GetDevice() };
                await _mqttEntityManager.CreateAsync($"{baseName}_started_at", new EntityCreationOptions(Name: $"{Name} - Started at", UniqueId: $"smartwasher_{Name}_started_at", DeviceClass: "timestamp", Persist: true), startedAtOptions);

                var etaOptions = new EntityOptions { Icon = "fapro:calendar-day", Device = GetDevice() };
                await _mqttEntityManager.CreateAsync($"{baseName}_eta", new EntityCreationOptions(Name: $"{Name} - ETA", UniqueId: $"smartwasher_{Name}_eta", DeviceClass: "timestamp", Persist: true), etaOptions);

                var lastStateChangeOptions = new EntityOptions { Icon = "fapro:calendar-day", Device = GetDevice() };
                await _mqttEntityManager.CreateAsync($"{baseName}_last_state_change", new EntityCreationOptions(Name: $"{Name} - Last change", UniqueId: $"smartwasher_{Name}_last_state_change", DeviceClass: "timestamp", Persist: true), lastStateChangeOptions);

                var progressOptions = new NumericSensorOptions { Icon = "mdi:progress-helper", Device = GetDevice(), UnitOfMeasurement = "%" };
                await _mqttEntityManager.CreateAsync($"{baseName}_progress", new EntityCreationOptions(Name: $"{Name} - Progress", UniqueId: $"smartwasher_{Name}_progress", Persist: true), progressOptions);

                var programOptions = new EntityOptions { Icon = "fapro:dial-med", Device = GetDevice() };
                await _mqttEntityManager.CreateAsync($"{baseName}_program", new EntityCreationOptions(UniqueId: $"{baseName}_program", Name: $"{Name} - Program", Persist: true), programOptions);
            }

            var switchObserver = await _mqttEntityManager.PrepareCommandSubscriptionAsync(switchName);
            SwitchDisposable = switchObserver.SubscribeAsync(EnabledSwitchHandler());

            var delayedStartObserver = await _mqttEntityManager.PrepareCommandSubscriptionAsync(delayedStartName);
            DelayedStartDisposable = delayedStartObserver.SubscribeAsync(DelayedStartSwitchHandler());

            var delayedStartTriggerObserver = await _mqttEntityManager.PrepareCommandSubscriptionAsync(delayedStartTriggerName);
            DelayedStartTriggerDisposable = delayedStartTriggerObserver.SubscribeAsync(DelayedStartTriggerHandler());
        }

        public Device GetDevice()
        {
            return new Device { Identifiers = new List<string> { $"smartwasher.{Name.MakeHaFriendly()}" }, Name = "Smartwasher: " + Name, Manufacturer = "Me" };
        }

        private void DelayedStartAwaken()
        {
            if (!CanDelayStart || _state is not DelayedStartState)
                return;

            TurnPowerSocketOn();
            _logger.LogDebug("{SmartWasher}: Awakening from delayed start.", Name);
        }

        internal Func<string, Task> DelayedStartTriggerHandler()
        {
            return async state =>
            {
                _logger.LogDebug("{SmartWasher}: Setting delayed start trigger to {state}.", Name, state);
                DelayedStartTriggered = state == "ON";

                if (DelayedStartTriggered)
                    DelayedStartAwaken();

                await UpdateStateInHomeAssistant();
            };
        }

        private Func<string, Task> DelayedStartSwitchHandler()
        {
            return async state =>
            {
                _logger.LogDebug("{SmartWasher}: Setting delayed start to {state}.", Name, state);
                CanDelayStart = state == "ON";
                await UpdateStateInHomeAssistant();
            };
        }

        private Func<string, Task> EnabledSwitchHandler()
        {
            return async state =>
            {
                _logger.LogDebug("{SmartWasher}: Setting smart washer switch state to {state}.", Name, state);
                if (state == "OFF")
                {
                    _logger.LogDebug("{SmartWasher}: Clearing smart washer switch state because it was disabled.", Name);
                }
                Enabled = state == "ON";
                await UpdateStateInHomeAssistant();
            };
        }

        private async Task UpdateStateInHomeAssistant()
        {
            var switchName = $"switch.smartwasher_{Name.MakeHaFriendly()}";
            var baseName = $"sensor.smartwasher_{Name.MakeHaFriendly()}";

            var attributes = new EnabledSwitchAttributes
            {
                LastUpdated = DateTime.Now.ToString("O"),
                Icon = "mdi:washing-machine"
            };

            await _mqttEntityManager.SetAttributesAsync(switchName, attributes);
            await _mqttEntityManager.SetStateAsync(switchName, Enabled ? "ON" : "OFF");
            await _mqttEntityManager.SetStateAsync($"{switchName}_delayed_start", CanDelayStart ? "ON" : "OFF");
            await _mqttEntityManager.SetStateAsync($"{switchName}_delayed_start_activate", DelayedStartTriggered ? "ON" : "OFF");

            await _mqttEntityManager.SetStateAsync($"{baseName}_state", State.ToString());

            await _mqttEntityManager.SetStateAsync($"{baseName}_started_at", StartedAt?.ToString("O") ?? "None");
            await _mqttEntityManager.SetStateAsync($"{baseName}_progress", PercentageComplete?.ToString() ?? "None");
            await _mqttEntityManager.SetStateAsync($"{baseName}_eta", Eta?.ToString("O") ?? "None");
            await _mqttEntityManager.SetStateAsync($"{baseName}_program", Program?.ToString() ?? "None");
            await _mqttEntityManager.SetStateAsync($"{baseName}_last_state_change", LastStateChange?.ToString("O") ?? "None");

            _logger.LogTrace("{SmartWasher}: Updated smartwasher sensors in Home assistant.", Name);

            _fileStorage.Save("Smartwasher", Name.MakeHaFriendly(), ToFileStorage());
        }

        private WasherStates? RetrieveState()
        {
            var fileStorage = _fileStorage.Get<SmartWasherFileStorage>("Smartwasher", Name.MakeHaFriendly());

            if (fileStorage == null)
                return null;

            Enabled = fileStorage?.Enabled ?? false;
            CanDelayStart = fileStorage?.CanDelayStart ?? false;
            DelayedStartTriggered = fileStorage?.DelayedStartTriggered ?? false;
            StartedAt = fileStorage?.StartedAt;
            Eta = fileStorage?.Eta;
            var state = fileStorage?.State;
            Program = fileStorage?.Program;
            PercentageComplete = fileStorage?.PercentageComplete;
            _logger.LogDebug("{SmartWasher}: Retrieved smart washer state state.", Name);

            return state;
        }

        internal SmartWasherFileStorage ToFileStorage() => new()
        {
            Enabled = Enabled,
            CanDelayStart = CanDelayStart,
            DelayedStartTriggered = DelayedStartTriggered,
            State = State,
            Program = Program,
            StartedAt = StartedAt,
            Eta = Eta,
            PercentageComplete = PercentageComplete
        };


        private void PowerSensor_Changed(object? sender, NumericSensorEventArgs e)
        {
            if (!Enabled)
                return;

            if (e.New?.State == 0 && NoPowerResetter == null && State != WasherStates.Idle && State != WasherStates.DelayedStart && State != WasherStates.Ready)
            {
                _logger.LogInformation("Wonky washer detected! Will reset washer to Idle in 10 minutes if socket power usage remains 0.");
                NoPowerResetter = _scheduler.Schedule(TimeSpan.FromMinutes(10), () => TransitionTo(_logger, new ReadyState()));
            }

            if (e.New?.State != 0 && NoPowerResetter != null)
                NoPowerResetter.Dispose();

            _state?.PowerUsageChanged(_logger, _scheduler, this);
        }

        private void PowerSocket_TurnedOn(object? sender, BinarySensorEventArgs e)
        {
            if (!Enabled)
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
            logger.LogDebug(
                _state != null
                    ? $"{{SmartWasher}}: Transitioning from state {_state.GetType().Name.Replace("State", "")} to {state.GetType().Name.Replace("State", "")}"
                    : $"{{SmartWasher}}: Initialized in {state.GetType().Name.Replace("State", "")} state.", Name);

            LastStateChange = _scheduler.Now;
            _state = state;
            _state.Enter(logger, _scheduler, this);
            Eta = _state.GetEta(_logger, this);

            UpdateStateInHomeAssistant().RunSync();
        }

        internal void SetStartedAt()
        {
            StartedAt = _scheduler.Now;
        }
        internal void ClearStartedAt()
        {
            StartedAt = null;
            Program = null;
        }

        internal void CalculateProgress()
        {
            var currentProgress = PercentageComplete;
            if (StartedAt == null) return;
            if (Eta == null) return;

            var totalTime = Eta - StartedAt;
            var passedTime = _scheduler.Now - StartedAt;
            var percentageComplete = passedTime / totalTime * 100;

            PercentageComplete = percentageComplete > 100
                ? 100
                : Convert.ToInt32(percentageComplete);

            if (currentProgress != PercentageComplete)
                UpdateStateInHomeAssistant().RunSync();
        }

        internal void SetWasherProgram(ILogger logger, WasherProgram? program)
        {
            Program = program;

            PercentageComplete = program switch
            {
                null => 100,
                WasherProgram.Unknown => 0,
                _ => PercentageComplete
            };

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
