using eLime.NetDaemonApps.Config;
using eLime.NetDaemonApps.Config.EnergyManager;
using eLime.NetDaemonApps.Domain.EnergyManager;
using eLime.NetDaemonApps.Domain.Storage;
using eLime.NetDaemonApps.Tests.Helpers;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using System.Reactive.Concurrency;

#pragma warning disable CS8618, CS9264

namespace eLime.NetDaemonApps.Tests.EnergyManager.Builders;

public class EnergyManagerBuilder
{
    private readonly AppTestContext _testCtx;
    private readonly ILogger _logger;
    private readonly IMqttEntityManager _mqttEntityManager;
    private readonly IFileStorage _fileStorage;
    private readonly IScheduler _scheduler;

    private string _timezone;
    private string _solarProductionRemainingToday;

    private string? _phoneToNotify;


    internal GridConfig _grid { get; private set; }
    internal List<EnergyConsumerConfig> _consumers = [];
    internal BatteryManagerConfig _batteryManager;

    public EnergyManagerBuilder(AppTestContext testCtx, ILogger logger, IMqttEntityManager mqttEntityManager, IFileStorage fileStorage, IScheduler scheduler)
    {
        _testCtx = testCtx;
        _logger = logger;
        _mqttEntityManager = mqttEntityManager;
        _fileStorage = fileStorage;
        _scheduler = scheduler;

        _grid = new GridConfig
        {
            ImportEntity = "sensor.grid_import",
            ExportEntity = "sensor.grid_export",
            PeakImportEntity = "sensor.grid_peak_import",
            CurrentAverageDemandEntity = "sensor.grid_current_average_demand",
            VoltageEntity = "sensor.grid_voltage",
        };

        _batteryManager = new BatteryManagerConfig
        {
            TotalChargePowerSensor = "sensor.battery_total_charge_power",
            TotalDischargePowerSensor = "sensor.battery_total_discharge_power",
            Batteries = []
        };

        _solarProductionRemainingToday = "sensor.solar_production_remaining_today";
        _phoneToNotify = "brecht";
        _timezone = "Utc";
    }

    public EnergyManagerBuilder AddGridMonitor(GridConfig grid)
    {
        _grid = grid;

        return this;
    }


    public EnergyManagerBuilder AddConsumer(EnergyConsumerConfig consumer)
    {
        _consumers.Add(consumer);

        return this;
    }

    public EnergyManagerBuilder AddBattery(BatteryConfig battery)
    {
        _batteryManager.Batteries.Add(battery);

        return this;
    }

    public async Task<Domain.EnergyManager.EnergyManager> Build()
    {
        var energyManagerConfig = new EnergyManagerConfig
        {
            Timezone = _timezone,
            PhoneToNotify = _phoneToNotify,
            SolarProductionRemainingTodayEntity = _solarProductionRemainingToday,
            Grid = _grid,
            Consumers = _consumers,
            BatteryManager = _batteryManager,

        };
        var energyManagerConfiguration = new EnergyManagerConfiguration(_testCtx.HaContext, _logger, _scheduler, _fileStorage, _mqttEntityManager, energyManagerConfig, TimeSpan.Zero);
        var x = await Domain.EnergyManager.EnergyManager.Create(energyManagerConfiguration);

        return x;
    }
}