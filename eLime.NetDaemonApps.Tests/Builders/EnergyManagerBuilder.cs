using eLime.NetDaemonApps.Domain.EnergyManager;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.Storage;
using eLime.NetDaemonApps.Tests.Helpers;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.HassModel.Entities;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Tests.Builders;

public class EnergyManagerBuilder
{
    private readonly AppTestContext _testCtx;
    private readonly ILogger _logger;
    private readonly IMqttEntityManager _mqttEntityManager;
    private readonly IFileStorage _fileStorage;
    private readonly IScheduler _scheduler;

    private NumericEntity _gridVoltageSensor;
    private NumericSensor _gridPowerImportSensor;
    private NumericSensor _gridPowerExportSensor;
    private NumericEntity _peakimportSensor;

    private NumericEntity _remainingSolarProductionToday;
    private String? _phoneToNotify;


    private List<EnergyConsumer> _consumers;

    public EnergyManagerBuilder(AppTestContext testCtx, ILogger logger, IMqttEntityManager mqttEntityManager, IFileStorage fileStorage, IScheduler scheduler)
    {
        _testCtx = testCtx;
        _logger = logger;
        _mqttEntityManager = mqttEntityManager;
        _fileStorage = fileStorage;
        _scheduler = scheduler;

        _gridVoltageSensor = new NumericEntity(_testCtx.HaContext, "sensor.grid_voltage");
        _gridPowerImportSensor = NumericSensor.Create(_testCtx.HaContext, "sensor.electricity_meter_power_consumption_watt");
        _gridPowerExportSensor = NumericSensor.Create(_testCtx.HaContext, "sensor.electricity_meter_power_production_watt");
        _peakimportSensor = new NumericEntity(_testCtx.HaContext, "input_number.peak_consumption");

        _phoneToNotify = "brecht";

        _consumers = new List<EnergyConsumer> { };
    }

    public EnergyManagerBuilder AddConsumer(EnergyConsumer consumer)
    {
        _consumers.Add(consumer);

        return this;
    }

    public EnergyManager Build()
    {

        var x = new EnergyManager(_testCtx.HaContext, _logger, _scheduler, _mqttEntityManager, _fileStorage, new GridMonitor(_scheduler, _gridVoltageSensor, _gridPowerImportSensor, _gridPowerExportSensor, _peakimportSensor), _remainingSolarProductionToday, _consumers, _phoneToNotify, TimeSpan.Zero);
        return x;
    }
}