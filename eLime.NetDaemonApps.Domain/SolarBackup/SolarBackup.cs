﻿using eLime.NetDaemonApps.Domain.Entities.Buttons;
using eLime.NetDaemonApps.Domain.Entities.Scripts;
using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Domain.Mqtt;
using eLime.NetDaemonApps.Domain.SolarBackup.Clients;
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

        private IDisposable StartBackupButtonListener { get; set; }
        private IDisposable StartBackupSwitchListener { get; set; }
        private IDisposable? GuardTask { get; }

        private string SynologyMacAddress { get; }
        private string SynologyBroadcastAddress { get; }
        internal Script Script { get; set; }
        private Button ShutDownButton { get; }

        internal PveClient PveClient { get; set; }
        internal PbsClient PbsClient { get; set; }

        private readonly TimeSpan _minimumChangeInterval = TimeSpan.FromSeconds(20);


        public SolarBackup(ILogger logger, IHaContext haContext, IScheduler scheduler, IFileStorage fileStorage, IMqttEntityManager mqttEntityManager, string synologyMacAddress, string synologyBroadcastAddress, PveClient pveClient, PbsClient pbsClient, Button shutdownButton)
        {
            _logger = logger;
            _haContext = haContext;
            _scheduler = scheduler;
            _fileStorage = fileStorage;
            _mqttEntityManager = mqttEntityManager;

            Script = new Script(haContext);
            SynologyMacAddress = synologyMacAddress;
            SynologyBroadcastAddress = synologyBroadcastAddress;

            PveClient = pveClient;
            PbsClient = pbsClient;
            ShutDownButton = shutdownButton;

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

            TransitionTo(_logger, initState).RunSync();

            GuardTask = _scheduler.RunEvery(_minimumChangeInterval, _scheduler.Now, () =>
            {
                _state.CheckProgress(_logger, scheduler, this);
            });
        }

        private async Task EnsureSensorsExist()
        {
            var solarBackupButtonEntityOptions = new ButtonOptions() { Icon = "mdi:solar-power", Device = GetDevice(), PayloadPress = "START" };
            await _mqttEntityManager.CreateAsync("button.solar_backup_start", new EntityCreationOptions(Name: $"Start solar backup", Persist: true), solarBackupButtonEntityOptions);

            var solarBackupEntityOptions = new ButtonOptions() { Icon = "mdi:solar-power", Device = GetDevice(), };
            await _mqttEntityManager.CreateAsync("switch.solar_backup_start", new EntityCreationOptions(Name: $"Start solar backup", DeviceClass: "switch", Persist: true), solarBackupEntityOptions);

            var stateOptions = new EntityOptions { Icon = "mdi:progress-helper", Device = GetDevice() };
            await _mqttEntityManager.CreateAsync($"sensor.solar_backup_state", new EntityCreationOptions(Name: $"Solar backup state", UniqueId: $"sensor.solar_backup_state", Persist: true), stateOptions);
            var startedAtOptions = new EntityOptions { Icon = "fapro:calendar-day", Device = GetDevice() };
            await _mqttEntityManager.CreateAsync($"sensor.solar_backup_started_at", new EntityCreationOptions(Name: $"Solar backup Started at", UniqueId: $"sensor.solar_backup_started_at", DeviceClass: "timestamp", Persist: true), startedAtOptions);

            var delayedStartButtonTriggerObserver = await _mqttEntityManager.PrepareCommandSubscriptionAsync("button.solar_backup_start");
            StartBackupButtonListener = delayedStartButtonTriggerObserver.SubscribeAsync(StartBackupButtonTriggeredHandler());

            var delayedStartSwitchTriggerObserver = await _mqttEntityManager.PrepareCommandSubscriptionAsync("switch.solar_backup_start");
            StartBackupSwitchListener = delayedStartSwitchTriggerObserver.SubscribeAsync(StartBackupSwitchTriggeredHandler());
        }

        internal Func<string, Task> StartBackupButtonTriggeredHandler()
        {
            return async state =>
            {
                _logger.LogDebug("Solar backup: Setting setting start triggered to {state}.", state);

                if ((state == "START" || state == "ON") && State == SolarBackupStatus.Idle)
                    await StartBackup();

                await UpdateStateInHomeAssistant();
            };
        }
        internal Func<string, Task> StartBackupSwitchTriggeredHandler()
        {
            return async state =>
            {
                _logger.LogDebug("Solar backup: Setting setting start triggered to {state}.", state);

                if (state == "ON" && State == SolarBackupStatus.Idle)
                    await StartBackup();

                await UpdateStateInHomeAssistant();
            };
        }

        private Task StartBackup()
        {
            _logger.LogDebug("Solar backup is starting");
            return TransitionTo(_logger, new StartingBackupServerState());
        }

        internal void BootServer()
        {
            Script.WakeUpPc(new WakeUpPcParameters
            {
                MacAddress = SynologyMacAddress,
                BroadcastAddress = SynologyBroadcastAddress
            });
            SetStartedAt();
        }

        internal void ShutDownServer()
        {
            ShutDownButton.Press();
        }

        public Device GetDevice()
        {
            return new Device { Identifiers = ["solar_backup"], Name = "Solar backup", Manufacturer = "Me" };
        }

        private async Task UpdateStateInHomeAssistant()
        {
            await _mqttEntityManager.SetStateAsync($"sensor.solar_backup_state", State.ToString());
            await _mqttEntityManager.SetStateAsync($"sensor.solar_backup_started_at", StartedAt?.ToString("O") ?? "None");
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

        internal async Task TransitionTo(ILogger logger, SolarBackupState state)
        {
            logger.LogDebug(
                _state != null
                    ? $"Transitioning from state {_state.GetType().Name.Replace("State", "")} to {state.GetType().Name.Replace("State", "")}"
                    : $"Initialized in {state.GetType().Name.Replace("State", "")} state.");

            _state = state;

            await _state.Enter(logger, _scheduler, this);
            await UpdateStateInHomeAssistant();
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
            StartBackupButtonListener?.Dispose();
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