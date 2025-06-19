using eLime.NetDaemonApps.Config;
using eLime.NetDaemonApps.Config.EnergyManager;
using eLime.NetDaemonApps.Domain.EnergyManager2;
using eLime.NetDaemonApps.Domain.EnergyManager2.Configuration;
using eLime.NetDaemonApps.Domain.Storage;
using eLime.NetDaemonApps.Tests.Helpers;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Tests.Builders;

public class EnergyManager2Builder
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

    public EnergyManager2Builder(AppTestContext testCtx, ILogger logger, IMqttEntityManager mqttEntityManager, IFileStorage fileStorage, IScheduler scheduler)
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
        };
        _phoneToNotify = "brecht";
        _timezone = "Utc";
    }

    public EnergyManager2Builder AddGridMonitor(GridConfig grid)
    {
        _grid = grid;

        return this;
    }


    public EnergyManager2Builder AddConsumer(EnergyConsumerConfig consumer)
    {
        _consumers.Add(consumer);

        return this;
    }

    public EnergyManager2Builder AddBatteryManager(BatteryManagerConfig batteryManager)
    {
        _batteryManager = batteryManager;
        return this;
    }

    public async Task<EnergyManager2> Build()
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
        var x = await EnergyManager2.Create(energyManagerConfiguration);

        return x;
    }
}