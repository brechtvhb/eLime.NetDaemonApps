using eLime.NetDaemonApps.Config.SmartWasher;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.SmartWashers;
using eLime.NetDaemonApps.Domain.Storage;
using eLime.NetDaemonApps.Tests.Helpers;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Tests.Builders;

public class SmartWasherBuilder
{
    private readonly AppTestContext _testCtx;
    private readonly ILogger _logger;
    private readonly IMqttEntityManager _mqttEntityManager;
    private readonly IScheduler _scheduler;
    private readonly IFileStorage _fileStorage;

    private SmartWasherConfig _config;
    private BinarySwitch? _powerSocket;
    private NumericSensor? _powerSensor;

    public SmartWasherBuilder(AppTestContext testCtx, ILogger logger, IMqttEntityManager mqttEntityManager, IScheduler scheduler, IFileStorage fileStorage)
    {
        _testCtx = testCtx;
        _logger = logger;
        _mqttEntityManager = mqttEntityManager;
        _scheduler = scheduler;
        _fileStorage = fileStorage;
        _config = new SmartWasherConfig()
        {
            Name = "smartwasher",
            Enabled = true,
            PowerSocket = "switch.socket_washer",
            PowerSensor = "sensor.socket_washer_power"
        };
    }

    public SmartWasherBuilder WithPowerSocket(BinarySwitch powerSocket)
    {
        _powerSocket = powerSocket;
        return this;
    }

    public SmartWasherBuilder WithPowerSensor(NumericSensor powerSensor)
    {
        _powerSensor = powerSensor;
        return this;
    }
    public SmartWasher Build()
    {
        var powerSocket = _powerSocket ?? BinarySwitch.Create(_testCtx.HaContext, _config.PowerSocket);
        var powerSensor = _powerSensor ?? NumericSensor.Create(_testCtx.HaContext, _config.PowerSensor);

        var smartWasher = new SmartWasher(_logger, _testCtx.HaContext, _mqttEntityManager, _scheduler, _fileStorage, _config.Enabled ?? true, _config.Name, powerSocket, powerSensor);
        return smartWasher;
    }
}