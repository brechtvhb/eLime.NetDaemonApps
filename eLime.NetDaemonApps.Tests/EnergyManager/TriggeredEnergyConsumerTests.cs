using eLime.NetDaemonApps.Domain.Storage;
using eLime.NetDaemonApps.Tests.EnergyManager.Builders;
using eLime.NetDaemonApps.Tests.Helpers;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace eLime.NetDaemonApps.Tests.EnergyManager;

[TestClass]
public class TriggeredEnergyConsumerTests
{
    private AppTestContext _testCtx;
    private ILogger _logger;
    private IMqttEntityManager _mqttEntityManager;
    private IFileStorage _fileStorage;

    [TestInitialize]
    public void Init()
    {
        _testCtx = AppTestContext.Create(DateTime.Now);

        _logger = A.Fake<ILogger<Domain.EnergyManager.EnergyManager>>();
        _mqttEntityManager = A.Fake<IMqttEntityManager>();
        _fileStorage = A.Fake<IFileStorage>();
    }

    [TestMethod]
    public async Task Init_HappyFlow()
    {
        // Arrange
        var consumer = TriggeredEnergyConsumerBuilder.Irrigation.Build();

        var energyManager = await new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        //Act

        //Assert
        Assert.AreEqual("Irrigation", energyManager.Consumers.First().Name);
    }

    [TestMethod]
    public async Task StateChange_Triggers_TurnsOn()
    {
        // Arrange
        var consumer = TriggeredEnergyConsumerBuilder.Irrigation.Build();

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        _ = await builder.Build();
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "1000");
        _testCtx.TriggerStateChange(consumer.Triggered!.SocketEntity, "off");

        //Act
        _testCtx.TriggerStateChange(consumer.Triggered!.StateSensor, "Yes");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(6));

        //Assert
        _testCtx.VerifySwitchTurnOn(consumer.Triggered!.SocketEntity, Moq.Times.Once);
    }


    [TestMethod]
    public async Task StateChange_Triggers_Button()
    {
        // Arrange
        var consumer = TriggeredEnergyConsumerBuilder.Dryer.Build();

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        _ = await builder.Build();
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "1000");

        //Act
        _testCtx.TriggerStateChange(consumer.Triggered!.StateSensor, "waiting_to_start");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Assert
        _testCtx.VerifyButtonPressed(consumer.Triggered!.StartButton!, Moq.Times.Once);
    }

    //Retarded use case, no longer needed
    [TestMethod]
    public async Task StateChange_Triggers_Socket_Then_Button()
    {
        // Arrange
        var consumer = TriggeredEnergyConsumerBuilder.Dryer
            .WithSocket("switch.dryer_socket")
            .Build();
        _testCtx.TriggerStateChange(consumer.Triggered!.SocketEntity, "off");

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        _ = await builder.Build();
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "1000");

        //Act
        _testCtx.TriggerStateChange(consumer.Triggered!.StateSensor, "waiting_to_start");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(5));

        _testCtx.TriggerStateChange(consumer.Triggered!.SocketEntity, "on");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(20));

        //Assert
        _testCtx.VerifySwitchTurnOn(consumer.Triggered!.SocketEntity, Moq.Times.Once);
        _testCtx.VerifyButtonPressed(consumer.Triggered!.StartButton!, Moq.Times.Once);
    }


    [TestMethod]
    public async Task Running_Renders_PeakLoad()
    {
        // Arrange
        var consumer = TriggeredEnergyConsumerBuilder.Irrigation
            .Build();
        _testCtx.TriggerStateChange(consumer.Triggered!.SocketEntity, "off");

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        var energyManager = await builder.Build();
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "1000");

        //Act
        _testCtx.TriggerStateChange(consumer.Triggered!.SocketEntity, "on");
        _testCtx.TriggerStateChange(consumer.Triggered!.StateSensor, "Ongoing");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Assert
        Assert.AreEqual(700, energyManager.Consumers.First().PeakLoad);
    }


    [TestMethod]
    public async Task Prewashing_Renders_PeakLoad()
    {
        // Arrange
        var consumer = TriggeredEnergyConsumerBuilder.Washer
            .Build();
        _testCtx.TriggerStateChange(consumer.Triggered!.SocketEntity, "off");

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        var energyManager = await builder.Build();
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "1000");

        //Act
        _testCtx.TriggerStateChange(consumer.Triggered!.SocketEntity, "on");
        _testCtx.TriggerStateChange(consumer.Triggered!.StateSensor, "Prewashing");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Assert
        Assert.AreEqual(2200, energyManager.Consumers.First().PeakLoad);
    }


    [TestMethod]
    public async Task Heating_Renders_PeakLoad()
    {
        // Arrange
        var consumer = TriggeredEnergyConsumerBuilder.Washer
            .Build();
        _testCtx.TriggerStateChange(consumer.Triggered!.SocketEntity, "off");

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        var energyManager = await builder.Build();
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "1000");

        //Act
        _testCtx.TriggerStateChange(consumer.Triggered!.SocketEntity, "on");
        _testCtx.TriggerStateChange(consumer.Triggered!.StateSensor, "Heating");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Assert
        Assert.AreEqual(2200, energyManager.Consumers.First().PeakLoad);
    }


    [TestMethod]
    public async Task PostHeating_Renders_LowerPeakLoad()
    {
        // Arrange
        var consumer = TriggeredEnergyConsumerBuilder.Washer
            .Build();
        _testCtx.TriggerStateChange(consumer.Triggered!.SocketEntity, "off");

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        var energyManager = await builder.Build();
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "1000");

        //Act
        _testCtx.TriggerStateChange(consumer.Triggered!.SocketEntity, "on");
        _testCtx.TriggerStateChange(consumer.Triggered!.StateSensor, "Washing");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Assert
        Assert.AreEqual(420, energyManager.Consumers.First().PeakLoad);
    }

    [TestMethod]
    public async Task StateChange_Triggers_TurnsOff()
    {
        var consumer = TriggeredEnergyConsumerBuilder.Washer
            .Build();
        _testCtx.TriggerStateChange(consumer.Triggered!.SocketEntity, "off");

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        var energyManager = await builder.Build();

        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "1000");
        _testCtx.TriggerStateChange(consumer.Triggered!.SocketEntity, "on");
        _testCtx.TriggerStateChange(consumer.Triggered!.StateSensor, "Spinning");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Act
        _testCtx.TriggerStateChange(consumer.Triggered!.StateSensor, "Ready");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(20));

        //Assert
        _testCtx.VerifySwitchTurnOff(consumer.Triggered!.SocketEntity, Moq.Times.AtLeastOnce);
    }

    [TestMethod]
    public async Task PeakLoad_Triggers_Pause()
    {
        // Arrange
        var consumer = TriggeredEnergyConsumerBuilder.Dishwasher
            .Build();

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        _ = await builder.Build();

        _testCtx.TriggerStateChange(consumer.Triggered!.SocketEntity, "on");
        _testCtx.TriggerStateChange(consumer.Triggered!.StateSensor, "Running");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Act
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "2500");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(5));

        //Assert
        _testCtx.VerifyButtonPressed(consumer.Triggered!.PauseButton!, Moq.Times.Once);
    }

    [TestMethod]
    public async Task ReducedLoad_Triggers_Resume()
    {
        // Arrange
        var consumer = TriggeredEnergyConsumerBuilder.Dishwasher
            .Build();

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        _ = await builder.Build();

        _testCtx.TriggerStateChange(consumer.Triggered!.SocketEntity, "on");
        _testCtx.TriggerStateChange(consumer.Triggered!.StateSensor, "Running");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "0");
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "3500");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(20));
        _testCtx.TriggerStateChange(consumer.Triggered!.StateSensor, "Paused");

        //Act
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "1000");
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "0");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(20));

        //Assert
        _testCtx.VerifyButtonPressed(consumer.Triggered!.StartButton!, Moq.Times.Once);
    }
}