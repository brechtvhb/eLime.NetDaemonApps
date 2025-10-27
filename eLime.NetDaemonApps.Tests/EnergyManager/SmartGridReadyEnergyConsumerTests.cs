using eLime.NetDaemonApps.Domain.SmartHeatPump;
using eLime.NetDaemonApps.Domain.Storage;
using eLime.NetDaemonApps.Tests.EnergyManager.Builders;
using eLime.NetDaemonApps.Tests.Helpers;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace eLime.NetDaemonApps.Tests.EnergyManager;

[TestClass]
public class SmartGridReadyEnergyConsumerTests
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
        var consumer = SmartGridReadyEnergyConsumerBuilder.HeatPump.Build();

        var energyManager = await new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        //Act

        //Assert
        Assert.AreEqual("Heat pump", energyManager.Consumers.First().Name);
    }

    [TestMethod]
    public async Task Renders_ExpectedPeakLoad_If_Available()
    {
        // Arrange
        var consumer = SmartGridReadyEnergyConsumerBuilder.HeatPump.Build();

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler).AddConsumer(consumer);
        var energyManager = await builder.Build();

        //Act
        _testCtx.TriggerStateChange(consumer.SmartGridReady!.ExpectedPeakLoadSensor, "2100");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Assert
        Assert.AreEqual(2100, energyManager.Consumers.First().PeakLoad);
    }


    [TestMethod]
    public async Task StateChange_Triggers_TurnsOn()
    {
        // Arrange
        var consumer = SmartGridReadyEnergyConsumerBuilder.HeatPump.Build();

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler).AddConsumer(consumer);
        _ = await builder.Build();
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "1500");

        //Act
        _testCtx.TriggerStateChange(consumer.SmartGridReady!.StateSensor, "Demanded");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(3));

        //Assert
        _testCtx.VerifySelectOptionPicked(consumer.SmartGridReady!.SmartGridModeEntity, SmartGridReadyMode.Boosted.ToString(), Moq.Times.Once);
    }

    [TestMethod]
    public async Task StateChange_Does_Not_Trigger_TurnOn_If_No_Export()
    {
        // Arrange
        var consumer = SmartGridReadyEnergyConsumerBuilder.HeatPump.Build();

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler).AddConsumer(consumer);
        _ = await builder.Build();
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "0");

        //Act
        _testCtx.TriggerStateChange(consumer.SmartGridReady!.StateSensor, "Demanded");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(3));

        //Assert
        _testCtx.VerifySelectOptionPicked(consumer.SmartGridReady!.SmartGridModeEntity, SmartGridReadyMode.Boosted.ToString(), Moq.Times.Never);
    }

    [TestMethod]
    public async Task StateChange_Does_Trigger_TurnsOn_If_No_Export_But_Critical()
    {
        // Arrange
        var consumer = SmartGridReadyEnergyConsumerBuilder.HeatPump.Build();

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler).AddConsumer(consumer);
        _ = await builder.Build();
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "0");

        //Act
        _testCtx.TriggerStateChange(consumer.SmartGridReady!.StateSensor, "CriticalDemand");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(3));

        //Assert
        _testCtx.VerifySelectOptionPicked(consumer.SmartGridReady!.SmartGridModeEntity, SmartGridReadyMode.Boosted.ToString(), Moq.Times.Once);
    }


    [TestMethod]
    public async Task StateChange_Triggers_TurnsOff()
    {
        var consumer = SmartGridReadyEnergyConsumerBuilder.HeatPump.Build();

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler).AddConsumer(consumer);
        _ = await builder.Build();

        _testCtx.TriggerStateChange(consumer.PowerUsageEntity, "1000");
        _testCtx.TriggerStateChange(consumer.SmartGridReady!.SmartGridModeEntity, "Boosted");
        _testCtx.TriggerStateChange(consumer.SmartGridReady!.StateSensor, "Demanded");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Act
        _testCtx.TriggerStateChange(consumer.SmartGridReady!.StateSensor, "NoDemand");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(5));

        //Assert
        _testCtx.VerifySelectOptionPicked(consumer.SmartGridReady!.SmartGridModeEntity, SmartGridReadyMode.Normal.ToString(), Moq.Times.AtLeastOnce);
    }

    [TestMethod]
    public async Task Blocks_During_BlockedWindow()
    {
        // Arrange
        _testCtx.SetCurrentTime(DateTime.Today.AddDays(1).AddHours(17));
        var consumer = SmartGridReadyEnergyConsumerBuilder.HeatPump.AddBlockedTimeWindow(null, [], TimeSpan.FromHours(16), TimeSpan.FromHours(18)).Build();

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler).AddConsumer(consumer);
        _ = await builder.Build();
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "0");

        //Act
        _testCtx.TriggerStateChange(consumer.SmartGridReady!.StateSensor, "Demanded");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(3));

        //Assert
        _testCtx.VerifySelectOptionPicked(consumer.SmartGridReady!.SmartGridModeEntity, SmartGridReadyMode.Blocked.ToString(), Moq.Times.Once);
    }

    [TestMethod]
    public async Task Blocks_During_BlockedWindow_On_A_Specific_Weekday()
    {
        // Arrange
        var today = DateTime.Today;
        var daysUntilSunday = ((int)DayOfWeek.Sunday - (int)today.DayOfWeek + 7) % 7;
        var firstSunday = today.AddDays(daysUntilSunday);

        _testCtx.SetCurrentTime(firstSunday.AddHours(17));
        var consumer = SmartGridReadyEnergyConsumerBuilder.HeatPump.AddBlockedTimeWindow(null, [DayOfWeek.Sunday], TimeSpan.FromHours(16), TimeSpan.FromHours(18)).Build();

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler).AddConsumer(consumer);
        _ = await builder.Build();
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "0");

        //Act
        _testCtx.TriggerStateChange(consumer.SmartGridReady!.StateSensor, "Demanded");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(3));

        //Assert
        _testCtx.VerifySelectOptionPicked(consumer.SmartGridReady!.SmartGridModeEntity, SmartGridReadyMode.Blocked.ToString(), Moq.Times.Once);
    }

    [TestMethod]
    public async Task Does_Not_Block_During_BlockedWindow_On_An_Unspecified_Weekday()
    {
        // Arrange
        var today = DateTime.Today;
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
        if (daysUntilMonday == 0)
            daysUntilMonday = 7;
        var firstMonday = today.AddDays(daysUntilMonday);

        _testCtx.SetCurrentTime(firstMonday.AddHours(17));
        var consumer = SmartGridReadyEnergyConsumerBuilder.HeatPump.AddBlockedTimeWindow(null, [DayOfWeek.Sunday], TimeSpan.FromHours(16), TimeSpan.FromHours(18)).Build();

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler).AddConsumer(consumer);
        _ = await builder.Build();
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "0");

        //Act
        _testCtx.TriggerStateChange(consumer.SmartGridReady!.StateSensor, "Demanded");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(3));

        //Assert
        _testCtx.VerifySelectOptionPicked(consumer.SmartGridReady!.SmartGridModeEntity, SmartGridReadyMode.Blocked.ToString(), Moq.Times.Never);
    }


    [TestMethod]
    public async Task Unblocks_Past_BlockedWindow()
    {
        // Arrange
        _testCtx.SetCurrentTime(DateTime.Today.AddDays(1).AddHours(17));
        var consumer = SmartGridReadyEnergyConsumerBuilder.HeatPump.AddBlockedTimeWindow(null, [], TimeSpan.FromHours(16), TimeSpan.FromHours(18)).Build();

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler).AddConsumer(consumer);
        _ = await builder.Build();
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "0");

        //Act
        _testCtx.TriggerStateChange(consumer.SmartGridReady!.StateSensor, "Demanded");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(3));
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "1500");
        _testCtx.AdvanceTimeBy(TimeSpan.FromHours(1));

        //Assert
        _testCtx.VerifySelectOptionPicked(consumer.SmartGridReady!.SmartGridModeEntity, SmartGridReadyMode.Normal.ToString(), Moq.Times.Once);
    }

    [TestMethod]
    public async Task Blocks_OnPeakLoad()
    {
        // Arrange
        _testCtx.SetCurrentTime(DateTime.Today.AddDays(1).AddHours(17));
        var consumer = SmartGridReadyEnergyConsumerBuilder.HeatPump.Build();

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler).AddConsumer(consumer);
        _ = await builder.Build();
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "0");
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "2600");
        _testCtx.TriggerStateChange(consumer.PowerUsageEntity, "1200");

        //Act
        _testCtx.TriggerStateChange(consumer.SmartGridReady!.StateSensor, "Demanded");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(10));

        //Assert
        _testCtx.VerifySelectOptionPicked(consumer.SmartGridReady!.SmartGridModeEntity, SmartGridReadyMode.Blocked.ToString(), Moq.Times.Once);
    }

    [TestMethod]
    public async Task UnBlocks_PastPeakLoad()
    {
        // Arrange
        _testCtx.SetCurrentTime(DateTime.Today.AddDays(1).AddHours(17));
        var consumer = SmartGridReadyEnergyConsumerBuilder.HeatPump.Build();

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler).AddConsumer(consumer);
        _ = await builder.Build();
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "0");
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "2600");
        _testCtx.TriggerStateChange(consumer.PowerUsageEntity, "1200");

        //Act
        _testCtx.TriggerStateChange(consumer.SmartGridReady!.StateSensor, "Demanded");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(10));
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "0");
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "1500");
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(16));

        //Assert
        _testCtx.VerifySelectOptionPicked(consumer.SmartGridReady!.SmartGridModeEntity, SmartGridReadyMode.Normal.ToString(), Moq.Times.Once);
    }

    [TestMethod]
    public async Task Rebalance_ScalesUp_From_Blocked_To_Normal_When_Energy_Available()
    {
        // Arrange
        _testCtx.SetCurrentTime(DateTime.Today.AddDays(1).AddHours(17));
        var consumer = SmartGridReadyEnergyConsumerBuilder.HeatPump.Build();

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler).AddConsumer(consumer);
        _ = await builder.Build();

        // Setup: Get into Blocked state first
        _testCtx.TriggerStateChange(consumer.SmartGridReady!.StateSensor, "Demanded");
        _testCtx.TriggerStateChange(consumer.SmartGridReady!.SmartGridModeEntity, "Blocked");
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "0");
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "100");
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(1));

        //Act - Make sufficient energy available
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "1500");
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "0");
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(16));

        //Assert
        _testCtx.VerifySelectOptionPicked(consumer.SmartGridReady!.SmartGridModeEntity, SmartGridReadyMode.Normal.ToString(), Moq.Times.AtLeastOnce);
    }

    [TestMethod]
    public async Task Rebalance_ScalesUp_From_Normal_To_Boosted_When_Energy_Available_And_Already_Running()
    {
        // Arrange
        _testCtx.SetCurrentTime(DateTime.Today.AddDays(1).AddHours(17));
        var consumer = SmartGridReadyEnergyConsumerBuilder.HeatPump.Build();

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler).AddConsumer(consumer);
        _ = await builder.Build();

        // Setup: Get into Normal state and running
        _testCtx.TriggerStateChange(consumer.SmartGridReady!.StateSensor, "Demanded");
        _testCtx.TriggerStateChange(consumer.SmartGridReady!.SmartGridModeEntity, "Normal");
        _testCtx.TriggerStateChange(consumer.PowerUsageEntity, "1000");
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "0");
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "100");
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(1));

        //Act - Make abundant energy available
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "2000");
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "0");
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(16));

        //Assert
        _testCtx.VerifySelectOptionPicked(consumer.SmartGridReady!.SmartGridModeEntity, SmartGridReadyMode.Boosted.ToString(), Moq.Times.AtLeastOnce);
    }

    [TestMethod]
    public async Task Rebalance_Does_Not_Scale_Up_Before_Minimum_Time_Passed()
    {
        // Arrange
        _testCtx.SetCurrentTime(DateTime.Today.AddDays(1).AddHours(17));
        var consumer = SmartGridReadyEnergyConsumerBuilder.HeatPump.Build();

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler).AddConsumer(consumer);
        _ = await builder.Build();

        // Setup: Get into Blocked state
        _testCtx.TriggerStateChange(consumer.SmartGridReady!.StateSensor, "Demanded");
        _testCtx.TriggerStateChange(consumer.SmartGridReady!.SmartGridModeEntity, "Blocked");
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "0");
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "100");
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(1));

        //Act - Make energy available but don't wait the required 15 minutes
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "1500");
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "0");
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(10));

        //Assert - Should not scale up yet
        _testCtx.VerifySelectOptionPicked(consumer.SmartGridReady!.SmartGridModeEntity, SmartGridReadyMode.Normal.ToString(), Moq.Times.Never);
    }

    [TestMethod]
    public async Task Rebalance_Does_Not_Scale_Up_From_Blocked_During_Blocked_Time_Window()
    {
        // Arrange
        _testCtx.SetCurrentTime(DateTime.Today.AddDays(1).AddHours(17));
        var consumer = SmartGridReadyEnergyConsumerBuilder.HeatPump.AddBlockedTimeWindow(null, [], TimeSpan.FromHours(16), TimeSpan.FromHours(18)).Build();

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler).AddConsumer(consumer);
        _ = await builder.Build();

        // Setup: Get into Blocked state during blocked time window
        _testCtx.TriggerStateChange(consumer.SmartGridReady!.StateSensor, "Demanded");
        _testCtx.TriggerStateChange(consumer.SmartGridReady!.SmartGridModeEntity, "Blocked");
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "0");
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "100");
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(1));

        //Act - Make energy available but still in blocked time window
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "1500");
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "0");
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(16));

        //Assert - Should not scale up during blocked time window
        _testCtx.VerifySelectOptionPicked(consumer.SmartGridReady!.SmartGridModeEntity, SmartGridReadyMode.Normal.ToString(), Moq.Times.Never);
    }

    [TestMethod]
    public async Task Rebalance_ScalesUp_From_Blocked_With_CriticallyNeedsEnergy_State()
    {
        // Arrange
        _testCtx.SetCurrentTime(DateTime.Today.AddDays(1).AddHours(17));
        var consumer = SmartGridReadyEnergyConsumerBuilder.HeatPump.Build();

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler).AddConsumer(consumer);
        _ = await builder.Build();

        // Setup: Get into Blocked state with Critical energy need
        _testCtx.TriggerStateChange(consumer.SmartGridReady!.StateSensor, "CriticalDemand");
        _testCtx.TriggerStateChange(consumer.SmartGridReady!.SmartGridModeEntity, "Blocked");
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "0");
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "100");
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(1));

        //Act - Critical demand with energy available under peak load
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "500");
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(16));

        //Assert - Should scale up even with some import as it's critical and under peak load
        _testCtx.VerifySelectOptionPicked(consumer.SmartGridReady!.SmartGridModeEntity, SmartGridReadyMode.Normal.ToString(), Moq.Times.AtLeastOnce);
    }
}