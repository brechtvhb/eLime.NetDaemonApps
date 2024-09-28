using eLime.NetDaemonApps.Domain.EnergyManager;
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

    private NumericEntity _remainingSolarProductionToday;

    public IGridMonitor _gridMonitor;

    private String? _phoneToNotify;


    private List<EnergyConsumer> _consumers;

    public EnergyManagerBuilder(AppTestContext testCtx, ILogger logger, IMqttEntityManager mqttEntityManager, IFileStorage fileStorage, IScheduler scheduler, IGridMonitor gridMonitor)
    {
        _testCtx = testCtx;
        _logger = logger;
        _mqttEntityManager = mqttEntityManager;
        _fileStorage = fileStorage;
        _scheduler = scheduler;
        _gridMonitor = gridMonitor;

        _phoneToNotify = "brecht";
        _consumers = [];
    }

    public EnergyManagerBuilder AddConsumer(EnergyConsumer consumer)
    {
        _consumers.Add(consumer);

        return this;
    }

    public EnergyManager Build()
    {

        var x = new EnergyManager(_testCtx.HaContext, _logger, _scheduler, _mqttEntityManager, _fileStorage, _gridMonitor, _remainingSolarProductionToday, _consumers, _phoneToNotify, TimeSpan.Zero);
        return x;
    }
}