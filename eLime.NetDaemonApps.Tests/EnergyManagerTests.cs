using eLime.NetDaemonApps.Domain.EnergyManager;
using eLime.NetDaemonApps.Tests.Builders;
using eLime.NetDaemonApps.Tests.Helpers;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.HassModel.Entities;
using EnergyManager = eLime.NetDaemonApps.Domain.EnergyManager.EnergyManager;

namespace eLime.NetDaemonApps.Tests;

[TestClass]
public class EnergyManagerTests
{
    private AppTestContext _testCtx;
    private ILogger _logger;
    private IMqttEntityManager _mqttEntityManager;

    [TestInitialize]
    public void Init()
    {
        _testCtx = AppTestContext.Create(DateTime.Now);

        _logger = A.Fake<ILogger<EnergyManager>>();
        _mqttEntityManager = A.Fake<IMqttEntityManager>();

        _testCtx.TriggerStateChange(new Entity(_testCtx.HaContext, "sensor.grid_voltage"), "230");
    }


    [TestMethod]
    public void Init_HappyFlow()
    {
        // Arrange
        var consumer = new SimpleEnergyConsumerBuilder(_testCtx)
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        //Act

        //Assert
        Assert.AreEqual("Pond pump", energyManager.Consumers.First().Name);
    }

    [TestMethod]
    public void Socket_Turning_on_Triggers_State_Running()
    {
        // Arrange
        var consumer = new SimpleEnergyConsumerBuilder(_testCtx)
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        //Act
        _testCtx.TriggerStateChange(consumer.Socket, "on");

        //Assert
        Assert.AreEqual(EnergyConsumerState.Running, energyManager.Consumers.First().State);
        Assert.AreEqual(_testCtx.Scheduler.Now, energyManager.Consumers.First().StartedAt);
        Assert.AreEqual(true, energyManager.Consumers.First().Running);
    }

    [TestMethod]
    public void Exporting_Energy_SwitchesOnLoad()
    {
        // Arrange
        var consumer = new SimpleEnergyConsumerBuilder(_testCtx)
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        //Act
        _testCtx.TriggerStateChange(energyManager.GridMonitor.GridPowerExportSensor, "2000");
        _testCtx.TriggerStateChange(energyManager.GridMonitor.GridPowerImportSensor, "0");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

        //Assert
        Assert.AreEqual(EnergyConsumerState.NeedsEnergy, energyManager.Consumers.First().State);
        _testCtx.VerifySwitchTurnOn(consumer.Socket, Moq.Times.Once);
    }
}