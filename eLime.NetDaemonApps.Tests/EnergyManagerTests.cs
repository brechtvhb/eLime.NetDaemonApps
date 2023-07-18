using eLime.NetDaemonApps.Domain.EnergyManager;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
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
        _testCtx.TriggerStateChange(new Entity(_testCtx.HaContext, "input_number.peak_consumption"), "4.0");
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
    public void Socket_Turning_off_Triggers_State_Off()
    {
        // Arrange
        var consumer = new SimpleEnergyConsumerBuilder(_testCtx)
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        _testCtx.TriggerStateChange(consumer.Socket, "on");
        _testCtx.TriggerStateChange(consumer.PowerUsage, "40");

        //Act
        _testCtx.TriggerStateChange(consumer.Socket, "off");

        //Assert
        Assert.AreEqual(EnergyConsumerState.NeedsEnergy, energyManager.Consumers.First().State);
        Assert.AreEqual(_testCtx.Scheduler.Now, energyManager.Consumers.First().LastRun);
        Assert.AreEqual(false, energyManager.Consumers.First().Running);
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
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Assert
        Assert.AreEqual(EnergyConsumerState.NeedsEnergy, energyManager.Consumers.First().State);
        _testCtx.VerifySwitchTurnOn(consumer.Socket, Moq.Times.Once);
    }

    [TestMethod]
    public void Above_Peak_Energy_SwitchesOffLoad()
    {
        // Arrange
        var consumer = new SimpleEnergyConsumerBuilder(_testCtx)
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        _testCtx.TriggerStateChange(consumer.Socket, "on");
        _testCtx.TriggerStateChange(consumer.PowerUsage, "40");

        //Act
        _testCtx.TriggerStateChange(energyManager.GridMonitor.GridPowerExportSensor, "0");
        _testCtx.TriggerStateChange(energyManager.GridMonitor.GridPowerImportSensor, "5000");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(31));

        //Assert
        _testCtx.VerifySwitchTurnOff(consumer.Socket, Moq.Times.Once);
    }

    [TestMethod]
    public void Above_Peak_Energy_WithMinimumRuntime_DoesNotSwitchOffLoad()
    {
        // Arrange
        var consumer = new SimpleEnergyConsumerBuilder(_testCtx)
            .WithRuntime(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(60))
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        _testCtx.TriggerStateChange(consumer.Socket, "on");
        _testCtx.TriggerStateChange(consumer.PowerUsage, "40");

        //Act
        _testCtx.TriggerStateChange(energyManager.GridMonitor.GridPowerExportSensor, "0");
        _testCtx.TriggerStateChange(energyManager.GridMonitor.GridPowerImportSensor, "5000");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(31));

        //Assert
        _testCtx.VerifySwitchTurnOff(consumer.Socket, Moq.Times.Never);
    }

    [TestMethod]
    public void Above_Peak_Energy_WithMinimumRuntime_SwitchesOffLoad_After_Duration()
    {
        // Arrange
        var consumer = new SimpleEnergyConsumerBuilder(_testCtx)
            .WithRuntime(TimeSpan.FromSeconds(55), TimeSpan.FromMinutes(60))
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        _testCtx.TriggerStateChange(consumer.Socket, "on");
        _testCtx.TriggerStateChange(consumer.PowerUsage, "40");

        //Act
        _testCtx.TriggerStateChange(energyManager.GridMonitor.GridPowerExportSensor, "0");
        _testCtx.TriggerStateChange(energyManager.GridMonitor.GridPowerImportSensor, "5000");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(61));

        //Assert
        _testCtx.VerifySwitchTurnOff(consumer.Socket, Moq.Times.Once);
    }

    [TestMethod]
    public void SwitchOffLoad_When_PreferSolar_After_MinimumRuntime()
    {
        // Arrange
        var consumer = new SimpleEnergyConsumerBuilder(_testCtx)
            .WithRuntime(TimeSpan.FromSeconds(55), TimeSpan.FromMinutes(60))
            .WithPreferSolar()
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        _testCtx.TriggerStateChange(consumer.Socket, "on");
        _testCtx.TriggerStateChange(consumer.PowerUsage, "40");

        //Act
        _testCtx.TriggerStateChange(energyManager.GridMonitor.GridPowerExportSensor, "0");
        _testCtx.TriggerStateChange(energyManager.GridMonitor.GridPowerImportSensor, "500");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(61));

        //Assert
        _testCtx.VerifySwitchTurnOff(consumer.Socket, Moq.Times.Once);
    }

    [TestMethod]
    public void DoNotSwitchOffLoad_When_PreferSolar_Before_MinimumRuntime()
    {
        // Arrange
        var consumer = new SimpleEnergyConsumerBuilder(_testCtx)
            .WithRuntime(TimeSpan.FromSeconds(90), TimeSpan.FromMinutes(60))
            .WithPreferSolar()
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        _testCtx.TriggerStateChange(consumer.Socket, "on");
        _testCtx.TriggerStateChange(consumer.PowerUsage, "40");

        //Act
        _testCtx.TriggerStateChange(energyManager.GridMonitor.GridPowerExportSensor, "0");
        _testCtx.TriggerStateChange(energyManager.GridMonitor.GridPowerImportSensor, "500");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(61));

        //Assert
        _testCtx.VerifySwitchTurnOff(consumer.Socket, Moq.Times.Never);
    }

    [TestMethod]
    public void MaximumRuntime_SwitchesOffLoad()
    {
        // Arrange
        var consumer = new SimpleEnergyConsumerBuilder(_testCtx)
            .WithRuntime(TimeSpan.FromSeconds(55), TimeSpan.FromSeconds(180))
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        _testCtx.TriggerStateChange(consumer.Socket, "on");
        _testCtx.TriggerStateChange(consumer.PowerUsage, "40");

        //Act
        _testCtx.TriggerStateChange(energyManager.GridMonitor.GridPowerExportSensor, "1000");
        _testCtx.TriggerStateChange(energyManager.GridMonitor.GridPowerImportSensor, "0");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(181));

        //Assert
        _testCtx.VerifySwitchTurnOff(consumer.Socket, Moq.Times.Once);
    }

    [TestMethod]
    public void Respects_Timeout()
    {
        // Arrange
        var consumer = new SimpleEnergyConsumerBuilder(_testCtx)
            .WithRuntime(TimeSpan.FromSeconds(55), TimeSpan.FromSeconds(180))
            .WithTimeout(TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(300))
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        _testCtx.TriggerStateChange(consumer.Socket, "on");
        _testCtx.TriggerStateChange(consumer.Socket, "off");

        //Act
        _testCtx.TriggerStateChange(energyManager.GridMonitor.GridPowerExportSensor, "1000");
        _testCtx.TriggerStateChange(energyManager.GridMonitor.GridPowerImportSensor, "0");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(31));

        //Assert
        _testCtx.VerifySwitchTurnOn(consumer.Socket, Moq.Times.Never);
    }

    [TestMethod]
    public void Can_Turn_On_After_Timeout()
    {
        // Arrange
        var consumer = new SimpleEnergyConsumerBuilder(_testCtx)
            .WithRuntime(TimeSpan.FromSeconds(55), TimeSpan.FromSeconds(180))
            .WithTimeout(TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(300))
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        _testCtx.TriggerStateChange(consumer.Socket, "on");
        _testCtx.TriggerStateChange(consumer.Socket, "off");

        //Act
        _testCtx.TriggerStateChange(energyManager.GridMonitor.GridPowerExportSensor, "1000");
        _testCtx.TriggerStateChange(energyManager.GridMonitor.GridPowerImportSensor, "0");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(61));

        //Assert
        _testCtx.VerifySwitchTurnOn(consumer.Socket, Moq.Times.Once);
    }

    [TestMethod]
    public void MultipleConsumers_SwitchOn_One_If_Not_Enough_Power()
    {
        // Arrange
        var consumer1 = new SimpleEnergyConsumerBuilder(_testCtx)
            .Build();
        var consumer2 = new SimpleEnergyConsumerBuilder(_testCtx)
            .WithLoad(-60, 100)
            .WithName("fridge")
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .AddConsumer(consumer1)
            .AddConsumer(consumer2)
            .Build();

        //Act
        _testCtx.TriggerStateChange(energyManager.GridMonitor.GridPowerExportSensor, "50");
        _testCtx.TriggerStateChange(energyManager.GridMonitor.GridPowerImportSensor, "0");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Assert
        _testCtx.VerifySwitchTurnOn(consumer1.Socket, Moq.Times.Once);
        _testCtx.VerifySwitchTurnOn(consumer2.Socket, Moq.Times.Never);
    }


    [TestMethod]
    public void MultipleConsumers_SwitchOn_Both_If_Enough_Power()
    {
        // Arrange
        var consumer1 = new SimpleEnergyConsumerBuilder(_testCtx)
            .Build();
        var consumer2 = new SimpleEnergyConsumerBuilder(_testCtx)
            .WithLoad(-60, 100)
            .WithName("fridge")
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .AddConsumer(consumer1)
            .AddConsumer(consumer2)
            .Build();

        //Act
        _testCtx.TriggerStateChange(energyManager.GridMonitor.GridPowerExportSensor, "200");
        _testCtx.TriggerStateChange(energyManager.GridMonitor.GridPowerImportSensor, "0");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Assert
        _testCtx.VerifySwitchTurnOn(consumer1.Socket, Moq.Times.Once);
        _testCtx.VerifySwitchTurnOn(consumer2.Socket, Moq.Times.Once);
    }


    [TestMethod]
    public void MultipleConsumers_Prioritizes_Critical()
    {
        // Arrange
        var consumer1 = new SimpleEnergyConsumerBuilder(_testCtx)
            .Build();
        var consumer2 = new SimpleEnergyConsumerBuilder(_testCtx)
            .WithLoad(-60, 100)
            .WithName("fridge")
            .WithCriticalSensor("boolean_sensor.fridge_too_hot")
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .AddConsumer(consumer1)
            .AddConsumer(consumer2)
            .Build();

        //Act
        _testCtx.TriggerStateChange(energyManager.GridMonitor.GridPowerExportSensor, "50");
        _testCtx.TriggerStateChange(energyManager.GridMonitor.GridPowerImportSensor, "0");
        _testCtx.TriggerStateChange(consumer2.CriticallyNeeded, "on");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Assert
        _testCtx.VerifySwitchTurnOn(consumer1.Socket, Moq.Times.Never);
        _testCtx.VerifySwitchTurnOn(consumer2.Socket, Moq.Times.Once);
    }


    [TestMethod]
    public void Exporting_Energy_SwitchesOnLoad_If_Within_TimeWindow()
    {
        _testCtx.SetCurrentTime(DateTime.Today.AddDays(1).AddHours(10));

        // Arrange
        var consumer = new SimpleEnergyConsumerBuilder(_testCtx)
            .AddTimeWindow(null, new TimeOnly(09, 00), new TimeOnly(12, 00))
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        //Act
        _testCtx.TriggerStateChange(energyManager.GridMonitor.GridPowerExportSensor, "2000");
        _testCtx.TriggerStateChange(energyManager.GridMonitor.GridPowerImportSensor, "0");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Assert
        Assert.AreEqual(EnergyConsumerState.NeedsEnergy, energyManager.Consumers.First().State);
        _testCtx.VerifySwitchTurnOn(consumer.Socket, Moq.Times.Once);
    }

    [TestMethod]
    public void Exporting_Energy_SwitchesOnLoad_If_Within_Active_TimeWindow()
    {
        _testCtx.SetCurrentTime(DateTime.Today.AddDays(1).AddHours(10));
        var active = new BinarySensor(_testCtx.HaContext, "binary_sensor.time_window_active");

        // Arrange
        var consumer = new SimpleEnergyConsumerBuilder(_testCtx)
            .AddTimeWindow(active, new TimeOnly(09, 00), new TimeOnly(12, 00))
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        //Act
        _testCtx.TriggerStateChange(energyManager.GridMonitor.GridPowerExportSensor, "2000");
        _testCtx.TriggerStateChange(energyManager.GridMonitor.GridPowerImportSensor, "0");
        _testCtx.TriggerStateChange(active, "on");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Assert
        Assert.AreEqual(EnergyConsumerState.NeedsEnergy, energyManager.Consumers.First().State);
        _testCtx.VerifySwitchTurnOn(consumer.Socket, Moq.Times.Once);
    }


    [TestMethod]
    public void Exporting_Energy_DoesNotSwitchOnLoad_If_Outside_TimeWindow()
    {
        _testCtx.SetCurrentTime(DateTime.Today.AddDays(1).AddHours(13));

        // Arrange
        var consumer = new SimpleEnergyConsumerBuilder(_testCtx)
            .AddTimeWindow(null, new TimeOnly(09, 00), new TimeOnly(12, 00))
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        //Act
        _testCtx.TriggerStateChange(energyManager.GridMonitor.GridPowerExportSensor, "2000");
        _testCtx.TriggerStateChange(energyManager.GridMonitor.GridPowerImportSensor, "0");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Assert
        Assert.AreEqual(EnergyConsumerState.NeedsEnergy, energyManager.Consumers.First().State);
        _testCtx.VerifySwitchTurnOn(consumer.Socket, Moq.Times.Never);
    }

    [TestMethod]
    public void Exporting_Energy_DoesNotSwitchOnLoad_If_TimeWindow_Not_Active()
    {
        _testCtx.SetCurrentTime(DateTime.Today.AddDays(1).AddHours(13));
        var active = new BinarySensor(_testCtx.HaContext, "binary_sensor.time_window_active");

        // Arrange
        var consumer = new SimpleEnergyConsumerBuilder(_testCtx)
            .AddTimeWindow(null, new TimeOnly(09, 00), new TimeOnly(12, 00))
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        //Act
        _testCtx.TriggerStateChange(energyManager.GridMonitor.GridPowerExportSensor, "2000");
        _testCtx.TriggerStateChange(energyManager.GridMonitor.GridPowerImportSensor, "0");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Assert
        Assert.AreEqual(EnergyConsumerState.NeedsEnergy, energyManager.Consumers.First().State);
        _testCtx.VerifySwitchTurnOn(consumer.Socket, Moq.Times.Never);
    }

    [TestMethod]
    public void Exporting_Energy_SwitchesOnLoad_If_Within_A_TimeWindow()
    {
        _testCtx.SetCurrentTime(DateTime.Today.AddDays(1).AddHours(10));

        // Arrange
        var consumer = new SimpleEnergyConsumerBuilder(_testCtx)
            .AddTimeWindow(null, new TimeOnly(09, 00), new TimeOnly(12, 00))
            .AddTimeWindow(null, new TimeOnly(13, 00), new TimeOnly(16, 00))
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        //Act
        _testCtx.TriggerStateChange(energyManager.GridMonitor.GridPowerExportSensor, "2000");
        _testCtx.TriggerStateChange(energyManager.GridMonitor.GridPowerImportSensor, "0");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Assert
        Assert.AreEqual(EnergyConsumerState.NeedsEnergy, energyManager.Consumers.First().State);
        _testCtx.VerifySwitchTurnOn(consumer.Socket, Moq.Times.Once);
    }


    [TestMethod]
    public void Exporting_Energy_DoesNotSwitchOnLoad_If_Outside__All_TimeWindows()
    {
        _testCtx.SetCurrentTime(DateTime.Today.AddDays(1).AddHours(13));

        // Arrange
        var consumer = new SimpleEnergyConsumerBuilder(_testCtx)
            .AddTimeWindow(null, new TimeOnly(09, 00), new TimeOnly(12, 00))
            .AddTimeWindow(null, new TimeOnly(13, 30), new TimeOnly(16, 00))
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        //Act
        _testCtx.TriggerStateChange(energyManager.GridMonitor.GridPowerExportSensor, "2000");
        _testCtx.TriggerStateChange(energyManager.GridMonitor.GridPowerImportSensor, "0");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Assert
        Assert.AreEqual(EnergyConsumerState.NeedsEnergy, energyManager.Consumers.First().State);
        _testCtx.VerifySwitchTurnOn(consumer.Socket, Moq.Times.Never);
    }
}