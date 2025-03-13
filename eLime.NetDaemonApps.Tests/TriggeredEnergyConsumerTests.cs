using eLime.NetDaemonApps.Domain.EnergyManager;
using eLime.NetDaemonApps.Domain.Storage;
using eLime.NetDaemonApps.Tests.Builders;
using eLime.NetDaemonApps.Tests.Helpers;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using EnergyManager = eLime.NetDaemonApps.Domain.EnergyManager.EnergyManager;

namespace eLime.NetDaemonApps.Tests;

[TestClass]
public class TriggeredEnergyConsumerTests
{
    private AppTestContext _testCtx;
    private ILogger _logger;
    private IMqttEntityManager _mqttEntityManager;
    private IFileStorage _fileStorage;
    private IGridMonitor _gridMonitor;

    [TestInitialize]
    public void Init()
    {
        _testCtx = AppTestContext.Create(DateTime.Now);

        _logger = A.Fake<ILogger<EnergyManager>>();
        _mqttEntityManager = A.Fake<IMqttEntityManager>();
        _fileStorage = A.Fake<IFileStorage>();
        _gridMonitor = A.Fake<IGridMonitor>();

        A.CallTo(() => _gridMonitor.PeakLoad).Returns(4000);
        A.CallTo(() => _gridMonitor.CurrentLoad).Returns(-2000);
        A.CallTo(() => _gridMonitor.AverageLoadSince(A<DateTimeOffset>._, A<TimeSpan>._)).Returns(-2000);
    }


    [TestMethod]
    public void Init_HappyFlow()
    {
        // Arrange
        var consumer = new TriggeredEnergyConsumerBuilder(_logger, _testCtx, "irrigation")
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler, _gridMonitor)
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
        var consumer = new TriggeredEnergyConsumerBuilder(_logger, _testCtx, "irrigation")
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler, _gridMonitor)
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
    public void StateChange_Triggers_Button()
    {
        // Arrange
        var consumer = new TriggeredEnergyConsumerBuilder(_logger, _testCtx, "tumble_dryer")
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler, _gridMonitor)
            .AddConsumer(consumer)
            .Build();

        //Act
        _testCtx.TriggerStateChange(consumer.StateSensor, "waiting_to_start");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Assert
        Assert.AreEqual(EnergyConsumerState.NeedsEnergy, energyManager.Consumers.First().State);
        _testCtx.VerifyButtonPressed(consumer.StartButton!, Moq.Times.Once);
    }


    [TestMethod]
    public void Running_Renders_PeakLoad()
    {
        // Arrange
        var consumer = new TriggeredEnergyConsumerBuilder(_logger, _testCtx, "irrigation")
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler, _gridMonitor)
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
        var consumer = new TriggeredEnergyConsumerBuilder(_logger, _testCtx, "washer")
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler, _gridMonitor)
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
    public void Heating_Renders_PeakLoad()
    {
        // Arrange
        var consumer = new TriggeredEnergyConsumerBuilder(_logger, _testCtx, "washer")
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler, _gridMonitor)
            .AddConsumer(consumer)
            .Build();

        //Act
        _testCtx.TriggerStateChange(consumer.Socket, "on");
        _testCtx.TriggerStateChange(consumer.StateSensor, "Heating");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Assert
        Assert.AreEqual(2200, energyManager.Consumers.First().PeakLoad);
    }


    [TestMethod]
    public void PostHeating_Renders_LowerPeakLoad()
    {
        // Arrange
        var consumer = new TriggeredEnergyConsumerBuilder(_logger, _testCtx, "washer")
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler, _gridMonitor)
            .AddConsumer(consumer)
            .Build();

        //Act
        _testCtx.TriggerStateChange(consumer.Socket, "on");
        _testCtx.TriggerStateChange(consumer.StateSensor, "Washing");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Assert
        Assert.AreEqual(420, energyManager.Consumers.First().PeakLoad);
    }

    [TestMethod]
    public void StateChange_Triggers_TurnsOff()
    {
        // Arrange
        var consumer = new TriggeredEnergyConsumerBuilder(_logger, _testCtx, "washer")
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler, _gridMonitor)
            .AddConsumer(consumer)
            .Build();

        _testCtx.TriggerStateChange(consumer.Socket, "on");
        _testCtx.TriggerStateChange(consumer.StateSensor, "Spinning");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Act
        _testCtx.TriggerStateChange(consumer.StateSensor, "Ready");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(20));

        //Assert
        Assert.AreEqual(EnergyConsumerState.Off, energyManager.Consumers.First().State);
        _testCtx.VerifySwitchTurnOff(consumer.Socket, Moq.Times.AtLeastOnce);
    }

    [TestMethod]
    public void PeakLoad_Triggers_Pause()
    {
        // Arrange
        var consumer = new TriggeredEnergyConsumerBuilder(_logger, _testCtx, "dishwasher")
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler, _gridMonitor)
            .AddConsumer(consumer)
            .Build();

        _testCtx.TriggerStateChange(consumer.Socket, "on");
        _testCtx.TriggerStateChange(consumer.StateSensor, "Running");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Act
        A.CallTo(() => _gridMonitor.CurrentLoad).Returns(3500);
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(20));

        //Assert
        _testCtx.VerifySwitchTurnOff(consumer.PauseSwitch, Moq.Times.AtLeastOnce);
    }

    [TestMethod]
    public void ReducedLoad_Triggers_Resume()
    {
        // Arrange
        var consumer = new TriggeredEnergyConsumerBuilder(_logger, _testCtx, "dishwasher")
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler, _gridMonitor)
            .AddConsumer(consumer)
            .Build();

        _testCtx.TriggerStateChange(consumer.Socket, "on");
        _testCtx.TriggerStateChange(consumer.StateSensor, "Running");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        _testCtx.TriggerStateChange(consumer.StateSensor, "Paused");
        A.CallTo(() => _gridMonitor.CurrentLoad).Returns(3500);
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(20));

        //Act
        A.CallTo(() => _gridMonitor.CurrentLoad).Returns(-1000);
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(20));

        //Assert
        _testCtx.VerifySwitchTurnOn(consumer.PauseSwitch, Moq.Times.AtLeastOnce);
    }
}