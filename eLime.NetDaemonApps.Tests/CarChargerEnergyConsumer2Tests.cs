using eLime.NetDaemonApps.Config.EnergyManager;
using eLime.NetDaemonApps.Domain.EnergyManager;
using eLime.NetDaemonApps.Domain.EnergyManager2.Consumers;
using eLime.NetDaemonApps.Domain.Storage;
using eLime.NetDaemonApps.Tests.Builders;
using eLime.NetDaemonApps.Tests.Helpers;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using EnergyManager = eLime.NetDaemonApps.Domain.EnergyManager.EnergyManager;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace eLime.NetDaemonApps.Tests;

[TestClass]
public class CarChargerEnergyConsumer2Tests
{
    private AppTestContext _testCtx;
    private ILogger _logger;
    private IMqttEntityManager _mqttEntityManager;
    private IFileStorage _fileStorage;

    [TestInitialize]
    public void Init()
    {
        _testCtx = AppTestContext.Create(DateTime.Now);

        _logger = A.Fake<ILogger<EnergyManager>>();
        _mqttEntityManager = A.Fake<IMqttEntityManager>();
        _fileStorage = A.Fake<IFileStorage>();
    }

    private void InitChargerState(EnergyConsumerConfig consumer, string state, int voltage, bool cableConnected, int batteryPercentage, string location, int? current)
    {
        _testCtx.TriggerStateChange(consumer.CarCharger!.StateSensor, state);
        _testCtx.TriggerStateChange(consumer.CarCharger!.VoltageEntity, voltage.ToString());
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().CableConnectedSensor, cableConnected ? "on" : "off");
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().BatteryPercentageSensor, batteryPercentage.ToString());
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().Location, location);
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        if (current != null)
            _testCtx.TriggerStateChange(consumer.CarCharger!.CurrentEntity, current.Value.ToString());
    }

    [TestMethod]
    public async Task Init_HappyFlow()
    {
        // Arrange
        var consumer = CarChargerEnergyConsumer2Builder.Passat().Build();

        var energyManager = await new EnergyManager2Builder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        //Act

        //Assert
        Assert.AreEqual("Veton", energyManager.Consumers.First().Name);
        Assert.AreEqual("Passat GTE", energyManager.Consumers.OfType<CarChargerEnergyConsumer2>().First().Cars.First().Name);
    }

    [TestMethod]
    public async Task Occupied_Triggers_TurnOn()
    {
        // Arrange
        var consumer = CarChargerEnergyConsumer2Builder.Passat().Build();

        var builder = new EnergyManager2Builder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        var energyManager = await builder.Build();

        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "5000");
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "0");

        //Act
        InitChargerState(consumer, "Occupied", 230, true, 5, "home", null);

        //Assert
        Assert.AreEqual(EnergyConsumerState.NeedsEnergy, energyManager.Consumers.First().State.State);
        _testCtx.InputNumberChanged(consumer.CarCharger!.CurrentEntity, 6, Moq.Times.Once);
    }

    [TestMethod]
    public async Task Occupied_ButNoKnownCar_DoesNotTrigger_TurnsOn()
    {
        // Arrange
        var consumer = CarChargerEnergyConsumer2Builder.Passat().Build();

        var builder = new EnergyManager2Builder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        var energyManager = await builder.Build();

        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "5000");
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "0");

        //Act
        InitChargerState(consumer, "Occupied", 230, false, 5, "home", null);

        //Assert
        Assert.AreEqual(EnergyConsumerState.Off, energyManager.Consumers.First().State.State);
        _testCtx.InputNumberChanged(consumer.CarCharger!.CurrentEntity, 6, Moq.Times.Never);
    }

    [TestMethod]
    public async Task Occupied_ButCarNotHome_DoesNotTrigger_TurnsOn()
    {
        // Arrange
        var consumer = CarChargerEnergyConsumer2Builder.Passat().Build();

        var builder = new EnergyManager2Builder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        var energyManager = await builder.Build();

        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "5000");
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "0");

        //Act
        InitChargerState(consumer, "Occupied", 230, false, 5, "away", null);

        //Assert
        Assert.AreEqual(EnergyConsumerState.Off, energyManager.Consumers.First().State.State);
        _testCtx.InputNumberChanged(consumer.CarCharger!.CurrentEntity, 6, Moq.Times.Never);
    }


    [TestMethod]
    public async Task Occupied_ButNotEnoughPower_DoesNotTrigger_TurnsOn()
    {
        // Arrange
        var consumer = CarChargerEnergyConsumer2Builder.Passat().Build();

        var builder = new EnergyManager2Builder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        var energyManager = await builder.Build();

        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "200");
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "0");

        //Act
        InitChargerState(consumer, "Occupied", 230, true, 5, "home", null);

        //Assert
        Assert.AreEqual(EnergyConsumerState.NeedsEnergy, energyManager.Consumers.First().State.State);
        _testCtx.InputNumberChanged(consumer.CarCharger!.CurrentEntity, 6, Moq.Times.Never);
    }

    [TestMethod]
    public async Task ExcessEnergy_Adjusts_Load()
    {
        // Arrange
        var consumer = CarChargerEnergyConsumer2Builder.Passat().Build();

        var builder = new EnergyManager2Builder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        _ = await builder.Build();

        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "0");
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "0");

        InitChargerState(consumer, "Charging", 230, true, 5, "home", 6);

        //Act
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "600");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

        //Assert
        _testCtx.InputNumberChanged(consumer.CarCharger!.CurrentEntity, 8, Moq.Times.Once);
    }

    [TestMethod]
    public async Task ConsumingEnergy_Adjusts_Load()
    {
        // Arrange
        var consumer = CarChargerEnergyConsumer2Builder.Passat().Build();

        var builder = new EnergyManager2Builder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        _ = await builder.Build();

        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "0");
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "0");

        InitChargerState(consumer, "Charging", 230, true, 5, "home", 16);

        //Act
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "800");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

        //Assert
        _testCtx.InputNumberChanged(consumer.CarCharger!.CurrentEntity, 12, Moq.Times.Once);
    }

    [TestMethod]
    public async Task ConsumingEnergy_Respects_MinimumTimeRuntime()
    {
        // Arrange
        var consumer = CarChargerEnergyConsumer2Builder.Passat()
            .WithRuntime(TimeSpan.FromMinutes(5), null)
            .Build();

        var builder = new EnergyManager2Builder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        var energyManager = await builder.Build();
        InitChargerState(consumer, "Charging", 230, true, 5, "home", 6);

        //Act
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "1200");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

        //Assert
        Assert.AreEqual(EnergyConsumerState.Running, energyManager.Consumers.First().State.State);
        _testCtx.InputNumberChanged(consumer.CarCharger!.CurrentEntity, 6, Moq.Times.Once);
        _testCtx.InputNumberChanged(consumer.CarCharger!.CurrentEntity, 5, Moq.Times.Never);
    }


    //[TestMethod]
    //public void ConsumingEnergy_ShutsDown_After_MinimumRuntime()
    //{
    //    // Arrange
    //    var consumer = new CarChargerEnergyConsumerBuilder(_logger, _testCtx)
    //        .WithRuntime(TimeSpan.FromMinutes(5), null)
    //        .Build();

    //    var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler, _gridMonitor)
    //        .AddConsumer(consumer)
    //        .Build();

    //    _testCtx.TriggerStateChange(consumer.StateSensor, "Occupied");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().CableConnectedSensor, "on");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "5");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().Location, "home");
    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

    //    //Act
    //    _testCtx.TriggerStateChange(consumer.StateSensor, "Charging");
    //    _testCtx.TriggerStateChange(consumer.CurrentEntity, "6");
    //    A.CallTo(() => _gridMonitor.CurrentLoadMinusBatteries).Returns(1200);
    //    A.CallTo(() => _gridMonitor.AverageLoadMinusBatteriesSince(A<DateTimeOffset>._, A<TimeSpan>._)).Returns(1200);
    //    _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(3));

    //    A.CallTo(() => _gridMonitor.CurrentLoadMinusBatteries).Returns(1201);
    //    A.CallTo(() => _gridMonitor.AverageLoadMinusBatteriesSince(A<DateTimeOffset>._, A<TimeSpan>._)).Returns(1201);
    //    _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(3));

    //    //Assert
    //    _testCtx.InputNumberChanged(consumer.CurrentEntity, 5, Times.Exactly(2));
    //}


    //[TestMethod]
    //public void ConsumingEnergy_IsNotJumpy()
    //{
    //    // Arrange
    //    var consumer = new CarChargerEnergyConsumerBuilder(_logger, _testCtx)
    //        .Build();

    //    var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler, _gridMonitor)
    //        .AddConsumer(consumer)
    //        .Build();

    //    _testCtx.TriggerStateChange(consumer.StateSensor, "Occupied");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().CableConnectedSensor, "on");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "5");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().Location, "home");
    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

    //    _testCtx.TriggerStateChange(consumer.CurrentEntity, "16");
    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(10));

    //    _testCtx.TriggerStateChange(consumer.StateSensor, "Charging");
    //    A.CallTo(() => _gridMonitor.CurrentLoadMinusBatteries).Returns(800);
    //    A.CallTo(() => _gridMonitor.AverageLoadMinusBatteriesSince(A<DateTimeOffset>._, A<TimeSpan>._)).Returns(800);
    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(25));

    //    //Act
    //    _testCtx.TriggerStateChange(consumer.CurrentEntity, "12");
    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(10));
    //    A.CallTo(() => _gridMonitor.CurrentLoadMinusBatteries).Returns(-110);
    //    A.CallTo(() => _gridMonitor.AverageLoadMinusBatteriesSince(A<DateTimeOffset>._, A<TimeSpan>._)).Returns(-110);
    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(25));

    //    //Assert
    //    _testCtx.InputNumberChanged(consumer.CurrentEntity, 12, Times.Once);
    //    _testCtx.InputNumberChanged(consumer.CurrentEntity, 11, Times.Never);
    //    _testCtx.InputNumberChanged(consumer.CurrentEntity, 13, Times.Never);
    //}

    //[TestMethod]
    //public void Charged_Triggers_TurnOff()
    //{
    //    // Arrange
    //    var consumer = new CarChargerEnergyConsumerBuilder(_logger, _testCtx)
    //        .Build();

    //    var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler, _gridMonitor)
    //        .AddConsumer(consumer)
    //        .Build();

    //    //Act
    //    _testCtx.TriggerStateChange(consumer.StateSensor, "Occupied");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().CableConnectedSensor, "on");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "5");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().Location, "home");

    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

    //    _testCtx.TriggerStateChange(consumer.StateSensor, "Charging");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "100");
    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

    //    //Assert
    //    Assert.AreEqual(EnergyConsumerState.Off, energyManager.Consumers.First().State);
    //    _testCtx.InputNumberChanged(consumer.CurrentEntity, 5, Times.Exactly(2));
    //}

    //[TestMethod]
    //public void MaxBatteryReached_Triggers_TurnOff()
    //{
    //    // Arrange
    //    var consumer = new CarChargerEnergyConsumerBuilder(_logger, _testCtx)
    //        .InitTeslaTests()
    //        .Build();

    //    var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler, _gridMonitor)
    //        .AddConsumer(consumer)
    //        .Build();

    //    //Act
    //    _testCtx.TriggerStateChange(consumer.StateSensor, "Occupied");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().ChargingStateSensor, "no_power");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().CableConnectedSensor, "on");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "5");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().MaxBatteryPercentageSensor, "80");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().Location, "home");

    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

    //    _testCtx.TriggerStateChange(consumer.StateSensor, "Charging");
    //    _testCtx.TriggerStateChange(consumer.CurrentEntity, "6");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().ChargingStateSensor, "complete");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "80");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().ChargerSwitch, "on");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().CurrentEntity, "2");
    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

    //    //Assert
    //    Assert.AreEqual(EnergyConsumerState.Off, energyManager.Consumers.First().State);
    //    _testCtx.VerifySwitchTurnOff(consumer.Cars.First().ChargerSwitch, Times.AtLeast(1));
    //}


    //[TestMethod]
    //public void MaxBatteryReached_DoesNotTriggerTurnOff_IfRemainOnAtFullBattery()
    //{
    //    // Arrange
    //    var consumer = new CarChargerEnergyConsumerBuilder(_logger, _testCtx)
    //        .InitTeslaTests(true)
    //        .Build();

    //    var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler, _gridMonitor)
    //        .AddConsumer(consumer)
    //        .Build();

    //    //Act
    //    _testCtx.TriggerStateChange(consumer.StateSensor, "Occupied");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().ChargingStateSensor, "no_power");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().CableConnectedSensor, "on");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "5");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().MaxBatteryPercentageSensor, "80");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().Location, "home");

    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

    //    _testCtx.TriggerStateChange(consumer.StateSensor, "Charging");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().ChargingStateSensor, "charging");
    //    _testCtx.TriggerStateChange(consumer.CurrentEntity, "6");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "80");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().ChargerSwitch, "on");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().CurrentEntity, "2");
    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

    //    //Assert
    //    Assert.AreEqual(EnergyConsumerState.Running, energyManager.Consumers.First().State);
    //    _testCtx.InputNumberChanged(consumer.CurrentEntity, 5, Moq.Times.Exactly(1));
    //    _testCtx.VerifySwitchTurnOff(consumer.Cars.First().ChargerSwitch, Times.Never);
    //}

    //[TestMethod]
    //public void ExcessEnergy_Adjusts_Load_3Phase()
    //{
    //    // Arrange
    //    var consumer = new CarChargerEnergyConsumerBuilder(_logger, _testCtx)
    //        .InitTeslaTests()
    //        .Build();

    //    var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler, _gridMonitor)
    //        .AddConsumer(consumer)
    //        .Build();


    //    _testCtx.TriggerStateChange(consumer.StateSensor, "Occupied");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().ChargingStateSensor, "no_power");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().CableConnectedSensor, "on");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "5");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().MaxBatteryPercentageSensor, "80");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().Location, "home");

    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

    //    _testCtx.TriggerStateChange(consumer.StateSensor, "Charging");
    //    _testCtx.TriggerStateChange(consumer.CurrentEntity, "6");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().ChargingStateSensor, "charging");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().CurrentEntity, "6");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().ChargerSwitch, "on");
    //    _testCtx.TriggerStateChange(consumer.PowerUsage, "4000");

    //    //Act
    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(15));
    //    A.CallTo(() => _gridMonitor.CurrentLoadMinusBatteries).Returns(800);
    //    A.CallTo(() => _gridMonitor.AverageLoadMinusBatteriesSince(A<DateTimeOffset>._, A<TimeSpan>._)).Returns(-900);
    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(20));

    //    //Assert
    //    _testCtx.InputNumberChanged(consumer.CurrentEntity, 16, Moq.Times.Once);
    //    _testCtx.InputNumberChanged(consumer.Cars.First().CurrentEntity, 7, Moq.Times.Once);
    //}


    //[TestMethod]
    //public void Occupied_Triggers_TurnOn_AndAdjustsCarCurrentForSupportedCar()
    //{
    //    // Arrange
    //    var consumer = new CarChargerEnergyConsumerBuilder(_logger, _testCtx)
    //        .InitTeslaTests()
    //        .Build();

    //    var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler, _gridMonitor)
    //        .AddConsumer(consumer)
    //        .Build();

    //    //Act
    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));
    //    _testCtx.TriggerStateChange(consumer.StateSensor, "Occupied");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().ChargingStateSensor, "no_power");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().CableConnectedSensor, "on");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "5");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().MaxBatteryPercentageSensor, "80");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().Location, "home");

    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(20));

    //    //Assert
    //    Assert.AreEqual(EnergyConsumerState.NeedsEnergy, energyManager.Consumers.First().State);
    //    _testCtx.InputNumberChanged(consumer.CurrentEntity, 16, Moq.Times.Once);

    //    _testCtx.VerifySwitchTurnOn(consumer.Cars.First().ChargerSwitch, Moq.Times.Once);
    //    _testCtx.InputNumberChanged(consumer.Cars.First().CurrentEntity, 1, Moq.Times.Once);
    //}

    //[TestMethod]
    //public void ExcessEnergy_Adjusts_Load_AndAdjustsCarCurrentForSupportedCar()
    //{
    //    // Arrange
    //    var consumer = new CarChargerEnergyConsumerBuilder(_logger, _testCtx)
    //        .InitTeslaTests()
    //        .Build();

    //    var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler, _gridMonitor)
    //        .AddConsumer(consumer)
    //        .Build();


    //    _testCtx.TriggerStateChange(consumer.StateSensor, "Occupied");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().ChargingStateSensor, "no_power");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().CableConnectedSensor, "on");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "5");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().MaxBatteryPercentageSensor, "80");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().Location, "home");

    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

    //    _testCtx.TriggerStateChange(consumer.StateSensor, "Charging");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().ChargingStateSensor, "charging");
    //    _testCtx.TriggerStateChange(consumer.CurrentEntity, "6");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().CurrentEntity, "1");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().ChargerSwitch, "on");
    //    _testCtx.TriggerStateChange(consumer.PowerUsage, "700");

    //    //Act
    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(15));
    //    A.CallTo(() => _gridMonitor.CurrentLoadMinusBatteries).Returns(-900);
    //    A.CallTo(() => _gridMonitor.AverageLoadMinusBatteriesSince(A<DateTimeOffset>._, A<TimeSpan>._)).Returns(-900);
    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(20));

    //    //Assert
    //    _testCtx.InputNumberChanged(consumer.Cars.First().CurrentEntity, 2, Moq.Times.Once);
    //}

    //[TestMethod]
    //public void ExcessEnergy_Adjusts_ChargerAndCarCurrentForSupportedCar()
    //{
    //    // Arrange
    //    var consumer = new CarChargerEnergyConsumerBuilder(_logger, _testCtx)
    //        .InitTeslaTests()
    //        .Build();

    //    var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler, _gridMonitor)
    //        .AddConsumer(consumer)
    //        .Build();

    //    _testCtx.TriggerStateChange(consumer.StateSensor, "Occupied");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().ChargingStateSensor, "no_power");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().CableConnectedSensor, "on");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "5");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().MaxBatteryPercentageSensor, "80");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().Location, "home");

    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

    //    _testCtx.TriggerStateChange(consumer.StateSensor, "Charging");
    //    _testCtx.TriggerStateChange(consumer.CurrentEntity, "6");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().ChargingStateSensor, "charging");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().CurrentEntity, "5");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().ChargerSwitch, "on");
    //    _testCtx.TriggerStateChange(consumer.PowerUsage, "3500");

    //    //Act
    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(10));
    //    A.CallTo(() => _gridMonitor.CurrentLoadMinusBatteries).Returns(-1500);
    //    A.CallTo(() => _gridMonitor.AverageLoadMinusBatteriesSince(A<DateTimeOffset>._, A<TimeSpan>._)).Returns(-1500);
    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(25));

    //    //Assert
    //    _testCtx.InputNumberChanged(consumer.CurrentEntity, 16, Times.Once);
    //    _testCtx.InputNumberChanged(consumer.Cars.First().CurrentEntity, 7, Times.Once);
    //}

    //[TestMethod]
    //public void ConsumingEnergy_Adjusts_ChargerAndCar()
    //{
    //    // Arrange
    //    var consumer = new CarChargerEnergyConsumerBuilder(_logger, _testCtx)
    //        .WithRuntime(TimeSpan.FromMinutes(5), null)
    //        .InitTeslaTests()
    //        .Build();

    //    var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler, _gridMonitor)
    //        .AddConsumer(consumer)
    //        .Build();

    //    _testCtx.TriggerStateChange(consumer.StateSensor, "Occupied");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().ChargingStateSensor, "no_power");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().CableConnectedSensor, "on");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "5");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().MaxBatteryPercentageSensor, "80");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().Location, "home");
    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

    //    _testCtx.TriggerStateChange(consumer.StateSensor, "Charging");
    //    _testCtx.TriggerStateChange(consumer.CurrentEntity, "8");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().ChargingStateSensor, "charging");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().CurrentEntity, "8");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().ChargerSwitch, "on");
    //    _testCtx.TriggerStateChange(consumer.PowerUsage, "5400");

    //    //Act
    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(10));
    //    A.CallTo(() => _gridMonitor.CurrentLoadMinusBatteries).Returns(900);
    //    A.CallTo(() => _gridMonitor.AverageLoadMinusBatteriesSince(A<DateTimeOffset>._, A<TimeSpan>._)).Returns(900);

    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(25));

    //    //Assert
    //    _testCtx.InputNumberChanged(consumer.CurrentEntity, 16, Moq.Times.Once);
    //    _testCtx.InputNumberChanged(consumer.Cars.First().CurrentEntity, 6, Moq.Times.Once);
    //}

    //[TestMethod]
    //public void ConsumingEnergy_Adjusts_OnlyCarWhenBelowChargerMinimumCurrent()
    //{
    //    // Arrange
    //    var consumer = new CarChargerEnergyConsumerBuilder(_logger, _testCtx)
    //        .WithRuntime(TimeSpan.FromMinutes(5), null)
    //        .InitTeslaTests()
    //        .Build();

    //    var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler, _gridMonitor)
    //        .AddConsumer(consumer)
    //        .Build();

    //    _testCtx.TriggerStateChange(consumer.StateSensor, "Occupied");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().ChargingStateSensor, "no_power");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().CableConnectedSensor, "on");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "5");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().MaxBatteryPercentageSensor, "80");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().Location, "home");

    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

    //    _testCtx.TriggerStateChange(consumer.StateSensor, "Charging");
    //    _testCtx.TriggerStateChange(consumer.CurrentEntity, "6");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().ChargingStateSensor, "charging");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().CurrentEntity, "6");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().ChargerSwitch, "on");
    //    _testCtx.TriggerStateChange(consumer.PowerUsage, "4000");

    //    //Act
    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(15));
    //    A.CallTo(() => _gridMonitor.CurrentLoadMinusBatteries).Returns(900);
    //    A.CallTo(() => _gridMonitor.AverageLoadMinusBatteriesSince(A<DateTimeOffset>._, A<TimeSpan>._)).Returns(900);

    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(20));

    //    //Assert
    //    _testCtx.InputNumberChanged(consumer.Cars.First().CurrentEntity, 4, Moq.Times.Once);
    //}

    //[TestMethod]
    //public void Balancing_Mode_Near_Peak_Load_Maximizes_Grid_Usage()
    //{
    //    // Arrange
    //    var consumer = new CarChargerEnergyConsumerBuilder(_logger, _testCtx)
    //        .WithRuntime(TimeSpan.FromMinutes(5), null)
    //        .InitTeslaTests()
    //        .Build();

    //    var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler, _gridMonitor)
    //        .AddConsumer(consumer)
    //        .Build();

    //    _testCtx.TriggerStateChange(consumer.StateSensor, "Occupied");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().ChargingStateSensor, "no_power");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().CableConnectedSensor, "on");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "5");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().MaxBatteryPercentageSensor, "80");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().Location, "home");
    //    consumer.SetBalancingMethod(_testCtx.Scheduler.Now, BalancingMethod.NearPeak);

    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

    //    _testCtx.TriggerStateChange(consumer.StateSensor, "Charging");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().ChargingStateSensor, "charging");
    //    _testCtx.TriggerStateChange(consumer.CurrentEntity, "16");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().CurrentEntity, "6");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().ChargerSwitch, "on");
    //    _testCtx.TriggerStateChange(consumer.PowerUsage, "4000");

    //    //Act
    //    A.CallTo(() => _gridMonitor.CurrentLoadMinusBatteries).Returns(800);
    //    A.CallTo(() => _gridMonitor.AverageLoadMinusBatteriesSince(A<DateTimeOffset>._, A<TimeSpan>._)).Returns(800);

    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

    //    //Assert
    //    _testCtx.InputNumberChanged(consumer.Cars.First().CurrentEntity, 10, Moq.Times.Once);
    //}

    //[TestMethod]
    //public void Balancing_Mode_Solar_Preferred_Load_Maximizes_Grid_Usage()
    //{
    //    // Arrange
    //    var consumer = new CarChargerEnergyConsumerBuilder(_logger, _testCtx)
    //        .WithRuntime(TimeSpan.FromMinutes(5), null)
    //        .InitTeslaTests()
    //        .Build();

    //    var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler, _gridMonitor)
    //        .AddConsumer(consumer)
    //        .Build();

    //    _testCtx.TriggerStateChange(consumer.StateSensor, "Occupied");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().ChargingStateSensor, "no_power");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().CableConnectedSensor, "on");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "5");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().MaxBatteryPercentageSensor, "80");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().Location, "home");

    //    consumer.SetBalancingMethod(_testCtx.Scheduler.Now, BalancingMethod.SolarPreferred);
    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

    //    _testCtx.TriggerStateChange(consumer.StateSensor, "Charging");
    //    _testCtx.TriggerStateChange(consumer.CurrentEntity, "16");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().ChargingStateSensor, "charging");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().CurrentEntity, "6");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().ChargerSwitch, "on");
    //    _testCtx.TriggerStateChange(consumer.PowerUsage, "4000");

    //    //Act
    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(10));
    //    A.CallTo(() => _gridMonitor.CurrentLoadMinusBatteries).Returns(-900);
    //    A.CallTo(() => _gridMonitor.AverageLoadMinusBatteriesSince(A<DateTimeOffset>._, A<TimeSpan>._)).Returns(-900);

    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(25));

    //    //Assert
    //    _testCtx.InputNumberChanged(consumer.Cars.First().CurrentEntity, 8, Moq.Times.Once);
    //}


    //[TestMethod]
    //public void Balancing_Mode_Solar_Only_Load_Minimizes_Grid_Usage()
    //{
    //    // Arrange
    //    var consumer = new CarChargerEnergyConsumerBuilder(_logger, _testCtx)
    //        .WithRuntime(TimeSpan.FromMinutes(5), null)
    //        .InitTeslaTests()
    //        .Build();

    //    var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler, _gridMonitor)
    //        .AddConsumer(consumer)
    //        .Build();

    //    _testCtx.TriggerStateChange(consumer.StateSensor, "Occupied");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().ChargingStateSensor, "no_power");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().CableConnectedSensor, "on");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "5");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().MaxBatteryPercentageSensor, "80");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().Location, "home");

    //    consumer.SetBalancingMethod(_testCtx.Scheduler.Now, BalancingMethod.SolarOnly);
    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

    //    _testCtx.TriggerStateChange(consumer.StateSensor, "Charging");
    //    _testCtx.TriggerStateChange(consumer.CurrentEntity, "16");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().ChargingStateSensor, "charging");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().CurrentEntity, "6");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().ChargerSwitch, "on");
    //    _testCtx.TriggerStateChange(consumer.PowerUsage, "4000");

    //    //Act
    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(10));
    //    A.CallTo(() => _gridMonitor.CurrentLoadMinusBatteries).Returns(-900);
    //    A.CallTo(() => _gridMonitor.AverageLoadMinusBatteriesSince(A<DateTimeOffset>._, A<TimeSpan>._)).Returns(-900);

    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(25));

    //    //Assert
    //    _testCtx.InputNumberChanged(consumer.Cars.First().CurrentEntity, 7, Moq.Times.Once);
    //}


    //[TestMethod]
    //public void Balancing_Mode_Solar_Surplus_Load_Minimizes_Grid_Usage()
    //{
    //    // Arrange
    //    var consumer = new CarChargerEnergyConsumerBuilder(_logger, _testCtx)
    //        .WithRuntime(TimeSpan.FromMinutes(5), null)
    //        .InitTeslaTests()
    //        .Build();

    //    var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler, _gridMonitor)
    //        .AddConsumer(consumer)
    //        .Build();

    //    _testCtx.TriggerStateChange(consumer.StateSensor, "Occupied");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().ChargingStateSensor, "no_power");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().CableConnectedSensor, "on");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "5");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().MaxBatteryPercentageSensor, "80");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().Location, "home");

    //    consumer.SetBalancingMethod(_testCtx.Scheduler.Now, BalancingMethod.SolarSurplus);
    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

    //    _testCtx.TriggerStateChange(consumer.StateSensor, "Charging");
    //    _testCtx.TriggerStateChange(consumer.CurrentEntity, "16");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().ChargingStateSensor, "charging");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().CurrentEntity, "6");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().ChargerSwitch, "on");
    //    _testCtx.TriggerStateChange(consumer.PowerUsage, "4000");

    //    //Act
    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(10));
    //    A.CallTo(() => _gridMonitor.CurrentLoadMinusBatteries).Returns(-1800);
    //    A.CallTo(() => _gridMonitor.AverageLoadMinusBatteriesSince(A<DateTimeOffset>._, A<TimeSpan>._)).Returns(-1800);

    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(25));

    //    //Assert
    //    _testCtx.InputNumberChanged(consumer.Cars.First().CurrentEntity, 7, Moq.Times.Once);
    //}


    //[TestMethod]
    //public void Balancing_Mode_Solar_Preferred_Load_Maximizes_Grid_Usage_But_IsNotJumpy()
    //{
    //    // Arrange
    //    var consumer = new CarChargerEnergyConsumerBuilder(_logger, _testCtx)
    //        .WithRuntime(TimeSpan.FromMinutes(5), null)
    //        .InitTeslaTests()
    //        .Build();

    //    var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler, _gridMonitor)
    //        .AddConsumer(consumer)
    //        .Build();

    //    _testCtx.TriggerStateChange(consumer.StateSensor, "Occupied");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().ChargingStateSensor, "no_power");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().CableConnectedSensor, "on");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "5");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().MaxBatteryPercentageSensor, "80");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().Location, "home");
    //    consumer.SetBalancingMethod(_testCtx.Scheduler.Now, BalancingMethod.SolarPreferred);

    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

    //    _testCtx.TriggerStateChange(consumer.StateSensor, "Charging");
    //    _testCtx.TriggerStateChange(consumer.CurrentEntity, "16");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().ChargingStateSensor, "charging");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().CurrentEntity, "6");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().ChargerSwitch, "on");
    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(10));
    //    _testCtx.TriggerStateChange(consumer.PowerUsage, "4000");
    //    A.CallTo(() => _gridMonitor.CurrentLoadMinusBatteries).Returns(-900);
    //    A.CallTo(() => _gridMonitor.AverageLoadMinusBatteriesSince(A<DateTimeOffset>._, A<TimeSpan>._)).Returns(-900);
    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(15));

    //    //Act
    //    _testCtx.TriggerStateChange(consumer.Cars.First().CurrentEntity, "8");
    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(10));
    //    _testCtx.TriggerStateChange(consumer.PowerUsage, "5400");
    //    A.CallTo(() => _gridMonitor.CurrentLoadMinusBatteries).Returns(600);
    //    A.CallTo(() => _gridMonitor.AverageLoadMinusBatteriesSince(A<DateTimeOffset>._, A<TimeSpan>._)).Returns(600);
    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(15));

    //    //Assert
    //    _testCtx.InputNumberChanged(consumer.Cars.First().CurrentEntity, 8, Moq.Times.Once);
    //    _testCtx.InputNumberChanged(consumer.Cars.First().CurrentEntity, 7, Moq.Times.Never);
    //    _testCtx.InputNumberChanged(consumer.Cars.First().CurrentEntity, 9, Moq.Times.Never);
    //}

    //[TestMethod]
    //public void Battery_ChargePower_Is_Included()
    //{
    //    // Arrange
    //    var consumer = new CarChargerEnergyConsumerBuilder(_logger, _testCtx)
    //        .WithRuntime(TimeSpan.FromMinutes(5), null)
    //        .InitTeslaTests()
    //        .Build();

    //    var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler, _gridMonitor)
    //        .AddConsumer(consumer)
    //        .Build();

    //    _testCtx.TriggerStateChange(consumer.StateSensor, "Occupied");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().ChargingStateSensor, "no_power");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().CableConnectedSensor, "on");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "5");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().MaxBatteryPercentageSensor, "80");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().Location, "home");
    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));
    //    consumer.SetBalancingMethod(_testCtx.Scheduler.Now, BalancingMethod.SolarOnly);
    //    consumer.SetAllowBatteryPower(AllowBatteryPower.Yes);

    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

    //    _testCtx.TriggerStateChange(consumer.StateSensor, "Charging");
    //    _testCtx.TriggerStateChange(consumer.CurrentEntity, "16");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().ChargingStateSensor, "charging");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().CurrentEntity, "6");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().ChargerSwitch, "on");
    //    _testCtx.TriggerStateChange(consumer.PowerUsage, "4000");

    //    //Act
    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(10));
    //    A.CallTo(() => _gridMonitor.CurrentLoad).Returns(0);
    //    A.CallTo(() => _gridMonitor.AverageLoadSince(A<DateTimeOffset>._, A<TimeSpan>._)).Returns(0);
    //    A.CallTo(() => _gridMonitor.CurrentLoadMinusBatteries).Returns(-1800);
    //    A.CallTo(() => _gridMonitor.AverageLoadMinusBatteriesSince(A<DateTimeOffset>._, A<TimeSpan>._)).Returns(-1800);
    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(15));

    //    //Assert
    //    _testCtx.InputNumberChanged(consumer.Cars.First().CurrentEntity, 8, Moq.Times.Never);
    //}

    //[TestMethod]
    //public void Battery_DischargePower_Is_Included()
    //{
    //    // Arrange
    //    var consumer = new CarChargerEnergyConsumerBuilder(_logger, _testCtx)
    //        .WithRuntime(TimeSpan.FromMinutes(5), null)
    //        .InitTeslaTests()
    //        .Build();

    //    var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler, _gridMonitor)
    //        .AddConsumer(consumer)
    //        .Build();

    //    _testCtx.TriggerStateChange(consumer.StateSensor, "Occupied");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().ChargingStateSensor, "no_power");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().CableConnectedSensor, "on");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "5");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().MaxBatteryPercentageSensor, "80");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().Location, "home");
    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));
    //    consumer.SetBalancingMethod(_testCtx.Scheduler.Now, BalancingMethod.SolarOnly);
    //    consumer.SetAllowBatteryPower(AllowBatteryPower.Yes);

    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

    //    _testCtx.TriggerStateChange(consumer.StateSensor, "Charging");
    //    _testCtx.TriggerStateChange(consumer.CurrentEntity, "16");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().ChargingStateSensor, "charging");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().CurrentEntity, "6");
    //    _testCtx.TriggerStateChange(consumer.Cars.First().ChargerSwitch, "on");
    //    _testCtx.TriggerStateChange(consumer.PowerUsage, "4000");

    //    //Act
    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(10));
    //    A.CallTo(() => _gridMonitor.CurrentLoad).Returns(-900);
    //    A.CallTo(() => _gridMonitor.AverageLoadSince(A<DateTimeOffset>._, A<TimeSpan>._)).Returns(-900);
    //    A.CallTo(() => _gridMonitor.CurrentLoadMinusBatteries).Returns(900);
    //    A.CallTo(() => _gridMonitor.AverageLoadMinusBatteriesSince(A<DateTimeOffset>._, A<TimeSpan>._)).Returns(900);
    //    _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(15));

    //    //Assert
    //    _testCtx.InputNumberChanged(consumer.Cars.First().CurrentEntity, 4, Moq.Times.Never);
    //}
}