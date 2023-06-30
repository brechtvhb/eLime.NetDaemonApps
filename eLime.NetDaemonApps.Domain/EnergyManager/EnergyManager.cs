using eLime.NetDaemonApps.Domain.Entities.Services;
using eLime.NetDaemonApps.Domain.Helper;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.EnergyManager;

public class EnergyManager : IDisposable
{
    public NumericEntity GridVoltageSensor { get; }
    public NumericEntity GridPowerImportSensor { get; }
    public NumericEntity GridPowerExportSensor { get; }
    public NumericEntity PeakImportSensor { get; }
    public NumericEntity SolarProductionRemainingTodaySensor { get; }
    public Int32 UnmonitoredAveragePowerConsumption { get; }

    public String? PhoneToNotify { get; }
    public Service Services { get; }

    private readonly List<EnergyConsumer> EnergyConsumers;
    private readonly IHaContext _haContext;
    private readonly ILogger _logger;
    private readonly IScheduler _scheduler;
    private readonly IMqttEntityManager _mqttEntityManager;

    private IDisposable? GuardTask { get; set; }

    public EnergyManager(IHaContext haContext, ILogger logger, IScheduler scheduler, IMqttEntityManager mqttEntityManager, NumericEntity gridVoltageSensor, NumericEntity gridPowerImportSensor, NumericEntity gridPowerExportSensor, NumericEntity peakImportSensor, NumericEntity solarProductionRemainingTodaySensor, int unmonitoredAveragePowerConsumption, List<EnergyConsumer> energyConsumers, string? phoneToNotify, TimeSpan debounceDuration)
    {
        _haContext = haContext;
        _logger = logger;
        _scheduler = scheduler;
        _mqttEntityManager = mqttEntityManager;


        GridVoltageSensor = gridVoltageSensor;
        GridPowerImportSensor = gridPowerImportSensor;
        GridPowerExportSensor = gridPowerExportSensor;
        PeakImportSensor = peakImportSensor;
        SolarProductionRemainingTodaySensor = solarProductionRemainingTodaySensor;
        UnmonitoredAveragePowerConsumption = unmonitoredAveragePowerConsumption;

        Services = new Service(_haContext);
        PhoneToNotify = phoneToNotify;

        EnergyConsumers = energyConsumers;

        //InitializeStateSensor().RunSync();
        //InitializeSolarEnergyAvailableSwitch().RunSync();

        foreach (var energyConsumer in EnergyConsumers)
        {
            //InitializeModeDropdown(wrapper).RunSync();
            //InitializeZoneStateSensor(wrapper).RunSync();
            energyConsumer.StateChanged += EnergyConsumer_StateChanged;
        }

        if (debounceDuration != TimeSpan.Zero)
        {
            StartConsumesrDebounceDispatcher = new(debounceDuration);
            StopConsumersDebounceDispatcher = new(debounceDuration);
        }

        //GuardTask = guardTask;
    }


    private void EnergyConsumer_StateChanged(object? sender, EnergyConsumerStateChangedEvent e)
    {
        var energyConsumer = EnergyConsumers.Single(x => x.Name == e.Consumer.Name);

        _logger.LogInformation("{EnergyConsumer}: State changed to: {State}.", e.Consumer.Name, e.State);

        switch (e)
        {
            case EnergyConsumerStartCommand:
                DebounceStartWatering();
                break;
            case EnergyConsumerStartedEvent:
                energyConsumer.Started(_logger, _scheduler);
                break;
            case EnergyConsumerStopCommand:
                energyConsumer.Stop();
                DebounceStartWatering();
                break;
            case EnergyConsumerStoppedEvent:
                energyConsumer.Stopped(_scheduler.Now);
                break;
        }

        //UpdateStateInHomeAssistant().RunSync();
    }

    //TODO: magic that keeps in mind current grid usage, peak usage and estimated solar production
    //TODO: Keep peak power usage of devices that are already running in mind
    private void StartConsumersIfNeeded()
    {
        Double estimatedLoad = GridPowerImportSensor.State - GridPowerExportSensor.State ?? 0;

        var consumersThatCriticallyNeedEnergy = EnergyConsumers.Where(x => x is { State: EnergyConsumerState.CriticallyNeedsEnergy });

        foreach (var criticalConsumer in consumersThatCriticallyNeedEnergy)
        {
            if (!criticalConsumer.CanStart(_scheduler.Now))
                continue;

            criticalConsumer.TurnOn();

            _logger.LogDebug("{Consumer}: Started consumer, consumer is in critical need of energy.", criticalConsumer.Name);
            estimatedLoad += criticalConsumer.PeakPowerUsage;
        }

        var consumersThatNeedEnergy = EnergyConsumers.Where(x => x is { State: EnergyConsumerState.NeedsEnergy });
        foreach (var consumer in consumersThatNeedEnergy)
        {
            if (!consumer.CanStart(_scheduler.Now))
                continue;

            consumer.TurnOn();

            _logger.LogDebug("{Consumer}: Will start consumer.", consumer.Name);
            estimatedLoad += consumer.PeakPowerUsage;
        }
    }
    private readonly DebounceDispatcher? StartConsumesrDebounceDispatcher;
    private readonly DebounceDispatcher? StopConsumersDebounceDispatcher;
    internal void DebounceStartWatering()
    {
        //if (StartConsumesrDebounceDispatcher == null)
        //{
        //    StartConsumersIfNeeded();
        //    return;
        //}

        //StartConsumesrDebounceDispatcher.Debounce(StartConsumersIfNeeded);
    }


    internal void DebounceStopWatering()
    {
        //if (StopConsumersDebounceDispatcher == null)
        //{
        //    StopWateringZonesIfNeeded();
        //    return;
        //}

        //StopConsumersDebounceDispatcher.Debounce(StopWateringZonesIfNeeded);
    }
    public void Dispose()
    {
        throw new NotImplementedException();
    }
}