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
public class TriggeredEnergyConsumerTests
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
        _testCtx.TriggerStateChange(new Entity(_testCtx.HaContext, "input_number.peak_consumption"), "4.0");
        _testCtx.TriggerStateChange(new Entity(_testCtx.HaContext, "sensor.electricity_meter_power_consumption_watt"), "0");
        _testCtx.TriggerStateChange(new Entity(_testCtx.HaContext, "sensor.electricity_meter_power_production_watt"), "2000");
    }


    [TestMethod]
    public void Init_HappyFlow()
    {
        // Arrange
        var consumer = new TriggeredEnergyConsumerBuilder(_testCtx, "irrigation")
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        //Act

        //Assert
        Assert.AreEqual("Irrigation", energyManager.Consumers.First().Name);
    }

    [TestMethod]
    public void StateChange_Triggers_TurnsOn()
    {
        // Arrange
        var consumer = new TriggeredEnergyConsumerBuilder(_testCtx, "irrigation")
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        //Act
        _testCtx.TriggerStateChange(consumer.StateSensor, "Yes");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Assert
        Assert.AreEqual(EnergyConsumerState.NeedsEnergy, energyManager.Consumers.First().State);
        _testCtx.VerifySwitchTurnOn(consumer.Socket, Moq.Times.Once);
    }

    [TestMethod]
    public void Running_Renders_PeakLoad()
    {
        // Arrange
        var consumer = new TriggeredEnergyConsumerBuilder(_testCtx, "irrigation")
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        //Act
        _testCtx.TriggerStateChange(consumer.Socket, "on");
        _testCtx.TriggerStateChange(consumer.StateSensor, "Ongoing");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Assert
        Assert.AreEqual(700, energyManager.Consumers.First().PeakLoad);
    }


    [TestMethod]
    public void Prewashing_Renders_PeakLoad()
    {
        // Arrange
        var consumer = new TriggeredEnergyConsumerBuilder(_testCtx, "washer")
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        //Act
        _testCtx.TriggerStateChange(consumer.Socket, "on");
        _testCtx.TriggerStateChange(consumer.StateSensor, "Prewashing");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Assert
        Assert.AreEqual(2200, energyManager.Consumers.First().PeakLoad);
    }

    [TestMethod]
    public void PostHeating_Renders_LowerPeakLoad()
    {
        // Arrange
        var consumer = new TriggeredEnergyConsumerBuilder(_testCtx, "washer")
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        //Act
        _testCtx.TriggerStateChange(consumer.Socket, "on");
        _testCtx.TriggerStateChange(consumer.StateSensor, "Washing");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Assert
        Assert.AreEqual(420, energyManager.Consumers.First().PeakLoad);
    }
}