using eLime.NetDaemonApps.Domain.EnergyManager;
using eLime.NetDaemonApps.Domain.Storage;
using eLime.NetDaemonApps.Tests.Builders;
using eLime.NetDaemonApps.Tests.Helpers;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using System.Globalization;
using EnergyManager = eLime.NetDaemonApps.Domain.EnergyManager.EnergyManager;

namespace eLime.NetDaemonApps.Tests;

[TestClass]
public class TriggeredEnergyConsumer2Tests
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
    }

    //TODO: Temp fix - should use last known values if value did not change in the wanted timeframe
    private void SimulateAveragePower(string? importEntity, double import, string? exportEntity, double export, int times, TimeSpan span)
    {
        do
        {
            var variance = Random.Shared.Next(-500, 500) / 100d;

            if (!string.IsNullOrEmpty(importEntity))
                _testCtx.TriggerStateChange(importEntity, (import + variance).ToString(CultureInfo.InvariantCulture));
            if (!string.IsNullOrEmpty(exportEntity))
                _testCtx.TriggerStateChange(exportEntity, (export + variance).ToString(CultureInfo.InvariantCulture));

            _testCtx.AdvanceTimeBy(span);
            times--;
        } while (times > 0);
    }

    [TestMethod]
    public async Task Init_HappyFlow()
    {
        // Arrange
        var consumer = TriggeredEnergyConsumer2Builder.Irrigation.Build();

        var energyManager = await new EnergyManager2Builder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
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
        var consumer = TriggeredEnergyConsumer2Builder.Irrigation.Build();

        var builder = new EnergyManager2Builder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
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
        var consumer = TriggeredEnergyConsumer2Builder.Dryer.Build();

        var builder = new EnergyManager2Builder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
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
        var consumer = TriggeredEnergyConsumer2Builder.Dryer
            .WithSocket("switch.dryer_socket")
            .Build();
        _testCtx.TriggerStateChange(consumer.Triggered!.SocketEntity, "off");

        var builder = new EnergyManager2Builder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        _ = await builder.Build();
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "1000");

        //Act
        _testCtx.TriggerStateChange(consumer.Triggered!.StateSensor, "waiting_to_start");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        _testCtx.TriggerStateChange(consumer.Triggered!.SocketEntity, "on");
        SimulateAveragePower(null, 0, builder._grid.ExportEntity, 1000, 2, TimeSpan.FromSeconds(10));

        //Assert
        _testCtx.VerifySwitchTurnOn(consumer.Triggered!.SocketEntity, Moq.Times.Once);
        _testCtx.VerifyButtonPressed(consumer.Triggered!.StartButton!, Moq.Times.Once);
    }


    [TestMethod]
    public async Task Running_Renders_PeakLoad()
    {
        // Arrange
        var consumer = TriggeredEnergyConsumer2Builder.Irrigation
            .Build();
        _testCtx.TriggerStateChange(consumer.Triggered!.SocketEntity, "off");

        var builder = new EnergyManager2Builder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
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
        var consumer = TriggeredEnergyConsumer2Builder.Washer
            .Build();
        _testCtx.TriggerStateChange(consumer.Triggered!.SocketEntity, "off");

        var builder = new EnergyManager2Builder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
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
        var consumer = TriggeredEnergyConsumer2Builder.Washer
            .Build();
        _testCtx.TriggerStateChange(consumer.Triggered!.SocketEntity, "off");

        var builder = new EnergyManager2Builder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
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
        var consumer = TriggeredEnergyConsumer2Builder.Washer
            .Build();
        _testCtx.TriggerStateChange(consumer.Triggered!.SocketEntity, "off");

        var builder = new EnergyManager2Builder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
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
        var consumer = TriggeredEnergyConsumer2Builder.Washer
            .Build();
        _testCtx.TriggerStateChange(consumer.Triggered!.SocketEntity, "off");

        var builder = new EnergyManager2Builder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
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
        Assert.AreEqual(EnergyConsumerState.Off, energyManager.Consumers.First().State.State);
        _testCtx.VerifySwitchTurnOff(consumer.Triggered!.SocketEntity, Moq.Times.AtLeastOnce);
    }

    [TestMethod]
    public async Task PeakLoad_Triggers_Pause()
    {
        // Arrange
        var consumer = TriggeredEnergyConsumer2Builder.Dishwasher
            .Build();

        var builder = new EnergyManager2Builder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        _ = await builder.Build();

        _testCtx.TriggerStateChange(consumer.Triggered!.SocketEntity, "on");
        _testCtx.TriggerStateChange(consumer.Triggered!.StateSensor, "Running");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Act
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "2500");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(5));

        //Assert
        _testCtx.VerifySwitchTurnOff(consumer.Triggered!.PauseSwitch!, Moq.Times.Once);
    }

    [TestMethod]
    public async Task ReducedLoad_Triggers_Resume()
    {
        // Arrange
        var consumer = TriggeredEnergyConsumer2Builder.Dishwasher
            .Build();

        var builder = new EnergyManager2Builder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        var energyManager = await builder.Build();

        _testCtx.TriggerStateChange(consumer.Triggered!.SocketEntity, "on");
        _testCtx.TriggerStateChange(consumer.Triggered!.StateSensor, "Running");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        SimulateAveragePower(builder._grid.ImportEntity, 3500, builder._grid.ExportEntity, 0, 1, TimeSpan.FromSeconds(10));
        _testCtx.TriggerStateChange(consumer.Triggered!.StateSensor, "Paused");

        //Act
        SimulateAveragePower(builder._grid.ImportEntity, 0, builder._grid.ExportEntity, 1000, 2, TimeSpan.FromSeconds(10));

        //Assert
        _testCtx.VerifySwitchTurnOn(consumer.Triggered!.PauseSwitch!, Moq.Times.Once);
    }
}