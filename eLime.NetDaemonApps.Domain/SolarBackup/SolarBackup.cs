using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Domain.Mqtt;
using eLime.NetDaemonApps.Domain.SolarBackup.States;
using eLime.NetDaemonApps.Domain.Storage;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.HassModel;
using System.Reactive.Concurrency;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("eLime.NetDaemonApps.Tests")]

namespace eLime.NetDaemonApps.Domain.SolarBackup
{
    public class SolarBackup : IDisposable
    {
        private readonly IHaContext _haContext;
        private readonly ILogger _logger;
        private readonly IScheduler _scheduler;
        private readonly IFileStorage _fileStorage;
        private readonly IMqttEntityManager _mqttEntityManager;

        private SolarBackupState _state;

        public SolarBackupStatus State => _state switch
        {
            IdleState => SolarBackupStatus.Idle,
            StartingBackupServerState => SolarBackupStatus.StartingBackupServer,
            BackingUpWorkloadState => SolarBackupStatus.BackingUpWorkload,
            BackingUpDataState => SolarBackupStatus.BackingUpData,
            VerifyingBackupsState => SolarBackupStatus.VerifyingBackups,
            PruningBackupsState => SolarBackupStatus.PruningBackups,
            GarbageCollectingState => SolarBackupStatus.GarbageCollecting,
            ShuttingDownBackupServerState => SolarBackupStatus.ShuttingDownBackupServer,
            ShuttingDownHardwareState => SolarBackupStatus.ShuttingDownHardware,
            _ => SolarBackupStatus.Idle
        };

        public DateTimeOffset? StartedAt { get; set; }

        private IDisposable StartBackupSwitchListener { get; set; }
        private IDisposable? GuardTask { get; }

        //PVE client & settings (storage id)
        private HttpClient _pveHttpClient { get; set; }

        //PBS client & settings (verify job id, prune job id)
        private HttpClient _pbsHttpClient { get; set; }
        //ShutdownButton

        private readonly TimeSpan _minimumChangeInterval = TimeSpan.FromSeconds(20);


        public SolarBackup(ILogger logger, IHaContext haContext, IScheduler scheduler, IFileStorage fileStorage, IMqttEntityManager mqttEntityManager)
        {
            _logger = logger;
            _haContext = haContext;
            _scheduler = scheduler;
            _fileStorage = fileStorage;
            _mqttEntityManager = mqttEntityManager;

            EnsureSensorsExist().RunSync();
            var state = RetrieveState();

            SolarBackupState initState = state switch
            {
                SolarBackupStatus.Idle => new IdleState(),
                SolarBackupStatus.StartingBackupServer => new StartingBackupServerState(),
                SolarBackupStatus.BackingUpWorkload => new BackingUpWorkloadState(),
                SolarBackupStatus.BackingUpData => new BackingUpDataState(),
                SolarBackupStatus.VerifyingBackups => new VerifyingBackupsState(),
                SolarBackupStatus.PruningBackups => new PruningBackupsState(),
                SolarBackupStatus.GarbageCollecting => new GarbageCollectingState(),
                SolarBackupStatus.ShuttingDownBackupServer => new ShuttingDownBackupServerState(),
                SolarBackupStatus.ShuttingDownHardware => new ShuttingDownHardwareState(),
                _ => new IdleState()
            };

            TransitionTo(_logger, initState);

            GuardTask = _scheduler.RunEvery(_minimumChangeInterval, _scheduler.Now, () =>
            {
                _state.CheckProgress(_logger, scheduler, this);
            });
        }

        private async Task EnsureSensorsExist()
        {
            var solarBackupEntityOptions = new EntityOptions { Icon = "mdi:solar-power", Device = GetDevice() };
            await _mqttEntityManager.CreateAsync("switch.solar_backup_start", new EntityCreationOptions(Name: $"Start solar backup", DeviceClass: "switch", Persist: true), solarBackupEntityOptions);

            var stateOptions = new EntityOptions { Icon = "mdi:progress-helper", Device = GetDevice() };
            await _mqttEntityManager.CreateAsync($"sensor.solar_backup_state", new EntityCreationOptions(Name: $"Solar backup state", UniqueId: $"sensor.solar_backup_state", Persist: true), stateOptions);
            var startedAtOptions = new EntityOptions { Icon = "fapro:calendar-day", Device = GetDevice() };
            await _mqttEntityManager.CreateAsync($"sensor.solar_backup_started_at", new EntityCreationOptions(Name: $"Solar backup Started at", UniqueId: $"sensor.solar_backup_started_at", DeviceClass: "timestamp", Persist: true), startedAtOptions);

            var delayedStartTriggerObserver = await _mqttEntityManager.PrepareCommandSubscriptionAsync("switch.solar_backup_start");
            StartBackupSwitchListener = delayedStartTriggerObserver.SubscribeAsync(StartBackupTriggeredHandler());
        }

        internal Func<string, Task> StartBackupTriggeredHandler()
        {
            return async state =>
            {
                _logger.LogDebug("Solar backup: Setting setting start triggered to {state}.", state);

                if (state == "ON" && State == SolarBackupStatus.Idle)
                    StartBackup();

                await UpdateStateInHomeAssistant();
            };
        }

        private void StartBackup()
        {
            _logger.LogDebug("Solar backup is starting");
            TransitionTo(_logger, new StartingBackupServerState());
        }

        public Device GetDevice()
        {
            return new Device { Identifiers = ["solar_backup"], Name = "Solar backup", Manufacturer = "Me" };
        }

        private async Task UpdateStateInHomeAssistant()
        {
            await _mqttEntityManager.SetStateAsync($"sensor.solar_backup_state", State.ToString());
            await _mqttEntityManager.SetStateAsync($"sensor.solar_backup_started_at", StartedAt?.ToString("O") ?? "None");
            await _mqttEntityManager.SetStateAsync($"switch.solar_backup_start", State == SolarBackupStatus.Idle ? "ON" : "OFF");
            _logger.LogTrace("Update solar backup sensors in home assistant.");

            _fileStorage.Save("SolarBackup", "SolarBackup", ToFileStorage());
        }

        private SolarBackupStatus? RetrieveState()
        {
            var fileStorage = _fileStorage.Get<SolarBackupFileStorage>("SolarBackup", "SolarBackup");

            if (fileStorage == null)
                return null;

            StartedAt = fileStorage?.StartedAt;
            var state = fileStorage?.State;
            _logger.LogDebug("Retrieved solar backup state.");

            return state;
        }

        internal SolarBackupFileStorage ToFileStorage() => new()
        {
            State = State,
            StartedAt = StartedAt,
        };

        internal void TransitionTo(ILogger logger, SolarBackupState state)
        {
            logger.LogDebug(
                _state != null
                    ? $"Transitioning from state {_state.GetType().Name.Replace("State", "")} to {state.GetType().Name.Replace("State", "")}"
                    : $"Initialized in {state.GetType().Name.Replace("State", "")} state.");

            _state = state;
            _state.Enter(logger, _scheduler, this);

            UpdateStateInHomeAssistant().RunSync();
        }

        internal void SetStartedAt()
        {
            StartedAt = _scheduler.Now;
        }
        internal void ClearStartedAt()
        {
            StartedAt = null;
        }

        public void Dispose()
        {
            _logger.LogInformation($"Solar backup disposing.");
            StartBackupSwitchListener?.Dispose();
            GuardTask?.Dispose();
            _logger.LogInformation($"Solar backup disposed.");
        }
    }
}

public enum SolarBackupStatus
{
    Idle,
    StartingBackupServer,
    BackingUpWorkload,
    BackingUpData,
    VerifyingBackups,
    PruningBackups,
    GarbageCollecting,
    ShuttingDownBackupServer,
    ShuttingDownHardware
}