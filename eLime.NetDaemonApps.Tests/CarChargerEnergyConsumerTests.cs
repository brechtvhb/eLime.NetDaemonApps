using eLime.NetDaemonApps.Domain.EnergyManager;
using eLime.NetDaemonApps.Domain.Storage;
using eLime.NetDaemonApps.Tests.Builders;
using eLime.NetDaemonApps.Tests.Helpers;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.HassModel.Entities;
using EnergyManager = eLime.NetDaemonApps.Domain.EnergyManager.EnergyManager;

namespace eLime.NetDaemonApps.Tests;

[TestClass]
public class CarChargerEnergyConsumerTests
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

        _testCtx.TriggerStateChange(new Entity(_testCtx.HaContext, "sensor.grid_voltage"), "230");
        _testCtx.TriggerStateChange(new Entity(_testCtx.HaContext, "input_number.peak_consumption"), "4.0");
        _testCtx.TriggerStateChange(new Entity(_testCtx.HaContext, "sensor.electricity_meter_power_consumption_watt"), "0");
        _testCtx.TriggerStateChange(new Entity(_testCtx.HaContext, "sensor.electricity_meter_power_production_watt"), "2000");
    }


    [TestMethod]
    public void Init_HappyFlow()
    {
        // Arrange
        var consumer = new CarChargerEnergyConsumerBuilder(_logger, _testCtx)
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        //Act

        //Assert
        Assert.AreEqual("Veton", energyManager.Consumers.First().Name);
        Assert.AreEqual("Passat GTE", energyManager.Consumers.OfType<CarChargerEnergyConsumer>().First().Cars.First().Name);
    }

    [TestMethod]
    public void Occupied_Triggers_TurnOn()
    {
        // Arrange
        var consumer = new CarChargerEnergyConsumerBuilder(_logger, _testCtx)
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        //Act
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));
        _testCtx.TriggerStateChange(consumer.StateSensor, "Occupied");
        _testCtx.TriggerStateChange(consumer.Cars.First().CableConnectedSensor, "on");
        _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "5");
        _testCtx.TriggerStateChange(consumer.Cars.First().Location, "home");

        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

        //Assert
        Assert.AreEqual(EnergyConsumerState.NeedsEnergy, energyManager.Consumers.First().State);
        _testCtx.InputNumberChanged(consumer.CurrentEntity, 6, Moq.Times.Once);
    }

    [TestMethod]
    public void Occupied_ButNoKnownCar_DoesNotTrigger_TurnsOn()
    {
        // Arrange
        var consumer = new CarChargerEnergyConsumerBuilder(_logger, _testCtx)
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        //Act
        _testCtx.TriggerStateChange(consumer.StateSensor, "Occupied");
        _testCtx.TriggerStateChange(consumer.Cars.First().CableConnectedSensor, "off");
        _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "5");

        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Assert
        Assert.AreEqual(EnergyConsumerState.Off, energyManager.Consumers.First().State);
        _testCtx.InputNumberChanged(consumer.CurrentEntity, 6, Moq.Times.Never);
    }

    [TestMethod]
    public void Occupied_ButCarNotHome_DoesNotTrigger_TurnsOn()
    {
        // Arrange
        var consumer = new CarChargerEnergyConsumerBuilder(_logger, _testCtx)
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        //Act
        _testCtx.TriggerStateChange(consumer.StateSensor, "Occupied");
        _testCtx.TriggerStateChange(consumer.Cars.First().CableConnectedSensor, "on");
        _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "5");
        _testCtx.TriggerStateChange(consumer.Cars.First().Location, "away");

        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Assert
        Assert.AreEqual(EnergyConsumerState.Off, energyManager.Consumers.First().State);
        _testCtx.InputNumberChanged(consumer.CurrentEntity, 6, Moq.Times.Never);
    }


    [TestMethod]
    public void Occupied_ButNotEnoughPower_DoesNotTrigger_TurnsOn()
    {
        // Arrange
        var consumer = new CarChargerEnergyConsumerBuilder(_logger, _testCtx)
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        //Act
        _testCtx.TriggerStateChange(new Entity(_testCtx.HaContext, "sensor.electricity_meter_power_production_watt"), "200");
        _testCtx.TriggerStateChange(consumer.StateSensor, "Occupied");
        _testCtx.TriggerStateChange(consumer.Cars.First().CableConnectedSensor, "on");
        _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "5");
        _testCtx.TriggerStateChange(consumer.Cars.First().Location, "home");

        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Assert
        Assert.AreEqual(EnergyConsumerState.NeedsEnergy, energyManager.Consumers.First().State);
        _testCtx.InputNumberChanged(consumer.CurrentEntity, 6, Moq.Times.Never);
    }

    [TestMethod]
    public void ExcessEnergy_Adjusts_Load()
    {
        // Arrange
        var consumer = new CarChargerEnergyConsumerBuilder(_logger, _testCtx)
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();


        _testCtx.TriggerStateChange(consumer.StateSensor, "Occupied");
        _testCtx.TriggerStateChange(consumer.Cars.First().CableConnectedSensor, "on");
        _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "5");
        _testCtx.TriggerStateChange(consumer.Cars.First().Location, "home");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Act
        _testCtx.TriggerStateChange(consumer.CurrentEntity, "6");
        _testCtx.TriggerStateChange(new Entity(_testCtx.HaContext, "sensor.electricity_meter_power_production_watt"), "600");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

        //Assert
        _testCtx.InputNumberChanged(consumer.CurrentEntity, 9, Moq.Times.Once);
    }

    [TestMethod]
    public void ConsumingEnergy_Adjusts_Load()
    {
        // Arrange
        var consumer = new CarChargerEnergyConsumerBuilder(_logger, _testCtx)
            .WithRuntime(TimeSpan.FromMinutes(5), null)
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        _testCtx.TriggerStateChange(consumer.StateSensor, "Occupied");
        _testCtx.TriggerStateChange(consumer.Cars.First().CableConnectedSensor, "on");
        _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "5");
        _testCtx.TriggerStateChange(consumer.Cars.First().Location, "home");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Act
        _testCtx.TriggerStateChange(consumer.CurrentEntity, "16");
        _testCtx.TriggerStateChange(new Entity(_testCtx.HaContext, "sensor.electricity_meter_power_production_watt"), "0");
        _testCtx.TriggerStateChange(new Entity(_testCtx.HaContext, "sensor.electricity_meter_power_consumption_watt"), "800");

        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

        //Assert
        _testCtx.InputNumberChanged(consumer.CurrentEntity, 13, Moq.Times.Once);
    }

    [TestMethod]
    public void ConsumingEnergy_Respects_MinimumTimeRuntime()
    {
        // Arrange
        var consumer = new CarChargerEnergyConsumerBuilder(_logger, _testCtx)
            .WithRuntime(TimeSpan.FromMinutes(5), null)
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));
        _testCtx.TriggerStateChange(consumer.StateSensor, "Occupied");
        _testCtx.TriggerStateChange(consumer.Cars.First().CableConnectedSensor, "on");
        _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "5");
        _testCtx.TriggerStateChange(consumer.Cars.First().Location, "home");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

        //Act
        _testCtx.TriggerStateChange(consumer.CurrentEntity, "6");
        _testCtx.TriggerStateChange(new Entity(_testCtx.HaContext, "sensor.electricity_meter_power_production_watt"), "0");
        _testCtx.TriggerStateChange(new Entity(_testCtx.HaContext, "sensor.electricity_meter_power_consumption_watt"), "1200");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

        //Assert
        Assert.AreEqual(EnergyConsumerState.Running, energyManager.Consumers.First().State);
        _testCtx.InputNumberChanged(consumer.CurrentEntity, 6, Moq.Times.Once);
        _testCtx.InputNumberChanged(consumer.CurrentEntity, 5, Moq.Times.Once);
    }

    [TestMethod]
    public void ConsumingEnergy_ShutsDown_After_MinimumRuntime()
    {
        // Arrange
        var consumer = new CarChargerEnergyConsumerBuilder(_logger, _testCtx)
            .WithRuntime(TimeSpan.FromMinutes(5), null)
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        _testCtx.TriggerStateChange(consumer.StateSensor, "Occupied");
        _testCtx.TriggerStateChange(consumer.Cars.First().CableConnectedSensor, "on");
        _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "5");
        _testCtx.TriggerStateChange(consumer.Cars.First().Location, "home");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Act
        _testCtx.TriggerStateChange(consumer.CurrentEntity, "6");
        _testCtx.TriggerStateChange(new Entity(_testCtx.HaContext, "sensor.electricity_meter_power_production_watt"), "0");
        _testCtx.TriggerStateChange(new Entity(_testCtx.HaContext, "sensor.electricity_meter_power_consumption_watt"), "1200");
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(3));

        _testCtx.TriggerStateChange(new Entity(_testCtx.HaContext, "sensor.electricity_meter_power_production_watt"), "0");
        _testCtx.TriggerStateChange(new Entity(_testCtx.HaContext, "sensor.electricity_meter_power_consumption_watt"), "1201");
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(3));

        //Assert
        Assert.AreEqual(EnergyConsumerState.NeedsEnergy, energyManager.Consumers.First().State);
        _testCtx.InputNumberChanged(consumer.CurrentEntity, 5, Moq.Times.Exactly(2));
    }


    [TestMethod]
    public void ConsumingEnergy_IsNotJumpy()
    {
        // Arrange
        var consumer = new CarChargerEnergyConsumerBuilder(_logger, _testCtx)
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        _testCtx.TriggerStateChange(consumer.StateSensor, "Occupied");
        _testCtx.TriggerStateChange(consumer.Cars.First().CableConnectedSensor, "on");
        _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "5");
        _testCtx.TriggerStateChange(consumer.Cars.First().Location, "home");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        _testCtx.TriggerStateChange(consumer.CurrentEntity, "16");
        _testCtx.TriggerStateChange(new Entity(_testCtx.HaContext, "sensor.electricity_meter_power_production_watt"), "0");
        _testCtx.TriggerStateChange(new Entity(_testCtx.HaContext, "sensor.electricity_meter_power_consumption_watt"), "800");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

        //Act
        _testCtx.TriggerStateChange(consumer.CurrentEntity, "13");
        _testCtx.TriggerStateChange(new Entity(_testCtx.HaContext, "sensor.electricity_meter_power_production_watt"), "0");
        _testCtx.TriggerStateChange(new Entity(_testCtx.HaContext, "sensor.electricity_meter_power_consumption_watt"), "110");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

        //Assert
        _testCtx.InputNumberChanged(consumer.CurrentEntity, 13, Moq.Times.Once);
        _testCtx.InputNumberChanged(consumer.CurrentEntity, 12, Moq.Times.Never);
        _testCtx.InputNumberChanged(consumer.CurrentEntity, 14, Moq.Times.Never);
    }

    [TestMethod]
    public void Charged_Triggers_TurnOff()
    {
        // Arrange
        var consumer = new CarChargerEnergyConsumerBuilder(_logger, _testCtx)
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        //Act
        _testCtx.TriggerStateChange(consumer.StateSensor, "Occupied");
        _testCtx.TriggerStateChange(consumer.Cars.First().CableConnectedSensor, "on");
        _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "5");
        _testCtx.TriggerStateChange(consumer.Cars.First().Location, "home");

        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        _testCtx.TriggerStateChange(consumer.StateSensor, "Charging");
        _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "100");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

        //Assert
        Assert.AreEqual(EnergyConsumerState.Off, energyManager.Consumers.First().State);
        _testCtx.InputNumberChanged(consumer.CurrentEntity, 5, Moq.Times.Exactly(2));
    }

    [TestMethod]
    public void MaxBatteryReached_Triggers_TurnOff()
    {
        // Arrange
        var consumer = new CarChargerEnergyConsumerBuilder(_logger, _testCtx)
            .InitTeslaTests()
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        //Act
        _testCtx.TriggerStateChange(consumer.StateSensor, "Occupied");
        _testCtx.TriggerStateChange(consumer.Cars.First().CableConnectedSensor, "on");
        _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "5");
        _testCtx.TriggerStateChange(consumer.Cars.First().MaxBatteryPercentageSensor, "80");
        _testCtx.TriggerStateChange(consumer.Cars.First().Location, "home");

        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        _testCtx.TriggerStateChange(consumer.StateSensor, "Charging");
        _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "80");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

        //Assert
        Assert.AreEqual(EnergyConsumerState.Off, energyManager.Consumers.First().State);
        _testCtx.InputNumberChanged(consumer.CurrentEntity, 5, Moq.Times.Exactly(2));
    }

    [TestMethod]
    public void ExcessEnergy_Adjusts_Load_3Phase()
    {
        // Arrange
        var consumer = new CarChargerEnergyConsumerBuilder(_logger, _testCtx)
            .InitTeslaTests()
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();


        _testCtx.TriggerStateChange(consumer.StateSensor, "Occupied");
        _testCtx.TriggerStateChange(consumer.Cars.First().CableConnectedSensor, "on");
        _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "5");
        _testCtx.TriggerStateChange(consumer.Cars.First().MaxBatteryPercentageSensor, "80");
        _testCtx.TriggerStateChange(consumer.Cars.First().Location, "home");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Act
        _testCtx.TriggerStateChange(consumer.CurrentEntity, "6");
        _testCtx.TriggerStateChange(consumer.Cars.First().CurrentEntity, "6");
        _testCtx.TriggerStateChange(new Entity(_testCtx.HaContext, "sensor.electricity_meter_power_production_watt"), "900");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

        //Assert
        _testCtx.InputNumberChanged(consumer.CurrentEntity, 16, Moq.Times.Once);
        _testCtx.InputNumberChanged(consumer.Cars.First().CurrentEntity, 8, Moq.Times.Once);
    }


    [TestMethod]
    public void Occupied_Triggers_TurnOn_AndAdjustsCarCurrentForSupportedCar()
    {
        // Arrange
        var consumer = new CarChargerEnergyConsumerBuilder(_logger, _testCtx)
            .InitTeslaTests()
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        //Act
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));
        _testCtx.TriggerStateChange(consumer.StateSensor, "Occupied");
        _testCtx.TriggerStateChange(consumer.Cars.First().CableConnectedSensor, "on");
        _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "5");
        _testCtx.TriggerStateChange(consumer.Cars.First().MaxBatteryPercentageSensor, "80");
        _testCtx.TriggerStateChange(consumer.Cars.First().Location, "home");

        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

        //Assert
        Assert.AreEqual(EnergyConsumerState.NeedsEnergy, energyManager.Consumers.First().State);
        _testCtx.InputNumberChanged(consumer.CurrentEntity, 16, Moq.Times.Once);
        _testCtx.InputNumberChanged(consumer.Cars.First().CurrentEntity, 1, Moq.Times.Once);
    }

    [TestMethod]
    public void ExcessEnergy_Adjusts_Load_AndAdjustsCarCurrentForSupportedCar()
    {
        // Arrange
        var consumer = new CarChargerEnergyConsumerBuilder(_logger, _testCtx)
            .InitTeslaTests()
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();


        _testCtx.TriggerStateChange(consumer.StateSensor, "Occupied");
        _testCtx.TriggerStateChange(consumer.Cars.First().CableConnectedSensor, "on");
        _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "5");
        _testCtx.TriggerStateChange(consumer.Cars.First().MaxBatteryPercentageSensor, "80");
        _testCtx.TriggerStateChange(consumer.Cars.First().Location, "home");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Act
        _testCtx.TriggerStateChange(consumer.CurrentEntity, "6");
        _testCtx.TriggerStateChange(consumer.Cars.First().CurrentEntity, "1");
        _testCtx.TriggerStateChange(new Entity(_testCtx.HaContext, "sensor.electricity_meter_power_production_watt"), "900");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

        //Assert
        _testCtx.InputNumberChanged(consumer.Cars.First().CurrentEntity, 3, Moq.Times.Once);
    }

    [TestMethod]
    public void ExcessEnergy_Adjusts_ChargerAndCarCurrentForSupportedCar()
    {
        // Arrange
        var consumer = new CarChargerEnergyConsumerBuilder(_logger, _testCtx)
            .InitTeslaTests()
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        _testCtx.TriggerStateChange(consumer.StateSensor, "Occupied");
        _testCtx.TriggerStateChange(consumer.Cars.First().CableConnectedSensor, "on");
        _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "5");
        _testCtx.TriggerStateChange(consumer.Cars.First().MaxBatteryPercentageSensor, "80");
        _testCtx.TriggerStateChange(consumer.Cars.First().Location, "home");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Act
        _testCtx.TriggerStateChange(consumer.CurrentEntity, "6");
        _testCtx.TriggerStateChange(consumer.Cars.First().CurrentEntity, "5");
        _testCtx.TriggerStateChange(new Entity(_testCtx.HaContext, "sensor.electricity_meter_power_production_watt"), "1500");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

        //Assert
        _testCtx.InputNumberChanged(consumer.CurrentEntity, 16, Moq.Times.Once);
        _testCtx.InputNumberChanged(consumer.Cars.First().CurrentEntity, 8, Moq.Times.Once);
    }

    [TestMethod]
    public void ConsumingEnergy_Adjusts_ChargerAndCar()
    {
        // Arrange
        var consumer = new CarChargerEnergyConsumerBuilder(_logger, _testCtx)
            .WithRuntime(TimeSpan.FromMinutes(5), null)
            .InitTeslaTests()
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        _testCtx.TriggerStateChange(consumer.StateSensor, "Occupied");
        _testCtx.TriggerStateChange(consumer.Cars.First().CableConnectedSensor, "on");
        _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "5");
        _testCtx.TriggerStateChange(consumer.Cars.First().MaxBatteryPercentageSensor, "80");
        _testCtx.TriggerStateChange(consumer.Cars.First().Location, "home");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Act
        _testCtx.TriggerStateChange(consumer.CurrentEntity, "8");
        _testCtx.TriggerStateChange(consumer.Cars.First().CurrentEntity, "8");
        _testCtx.TriggerStateChange(new Entity(_testCtx.HaContext, "sensor.electricity_meter_power_production_watt"), "0");
        _testCtx.TriggerStateChange(new Entity(_testCtx.HaContext, "sensor.electricity_meter_power_consumption_watt"), "800");

        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

        //Assert
        _testCtx.InputNumberChanged(consumer.CurrentEntity, 16, Moq.Times.Once);
        _testCtx.InputNumberChanged(consumer.Cars.First().CurrentEntity, 7, Moq.Times.Once);
    }

    [TestMethod]
    public void ConsumingEnergy_Adjusts_OnlyCarWhenBelowChargerMinimumCurrent()
    {
        // Arrange
        var consumer = new CarChargerEnergyConsumerBuilder(_logger, _testCtx)
            .WithRuntime(TimeSpan.FromMinutes(5), null)
            .InitTeslaTests()
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        _testCtx.TriggerStateChange(consumer.StateSensor, "Occupied");
        _testCtx.TriggerStateChange(consumer.Cars.First().CableConnectedSensor, "on");
        _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "5");
        _testCtx.TriggerStateChange(consumer.Cars.First().MaxBatteryPercentageSensor, "80");
        _testCtx.TriggerStateChange(consumer.Cars.First().Location, "home");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Act
        _testCtx.TriggerStateChange(consumer.CurrentEntity, "6");
        _testCtx.TriggerStateChange(consumer.Cars.First().CurrentEntity, "6");
        _testCtx.TriggerStateChange(new Entity(_testCtx.HaContext, "sensor.electricity_meter_power_production_watt"), "0");
        _testCtx.TriggerStateChange(new Entity(_testCtx.HaContext, "sensor.electricity_meter_power_consumption_watt"), "800");

        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

        //Assert
        _testCtx.InputNumberChanged(consumer.Cars.First().CurrentEntity, 5, Moq.Times.Once);
    }

    [TestMethod]
    public void Balancing_Mode_Near_Peak_Load_Maximizes_Grid_Usage()
    {
        // Arrange
        var consumer = new CarChargerEnergyConsumerBuilder(_logger, _testCtx)
            .WithRuntime(TimeSpan.FromMinutes(5), null)
            .InitTeslaTests()
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        _testCtx.TriggerStateChange(consumer.StateSensor, "Occupied");
        _testCtx.TriggerStateChange(consumer.Cars.First().CableConnectedSensor, "on");
        _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "5");
        _testCtx.TriggerStateChange(consumer.Cars.First().MaxBatteryPercentageSensor, "80");
        _testCtx.TriggerStateChange(consumer.Cars.First().Location, "home");
        consumer.BalancingMethod = BalancingMethod.NearPeak;

        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Act
        _testCtx.TriggerStateChange(consumer.CurrentEntity, "16");
        _testCtx.TriggerStateChange(consumer.Cars.First().CurrentEntity, "6");
        _testCtx.TriggerStateChange(new Entity(_testCtx.HaContext, "sensor.electricity_meter_power_production_watt"), "0");
        _testCtx.TriggerStateChange(new Entity(_testCtx.HaContext, "sensor.electricity_meter_power_consumption_watt"), "800");

        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

        //Assert
        _testCtx.InputNumberChanged(consumer.Cars.First().CurrentEntity, 10, Moq.Times.Once);
    }

    [TestMethod]
    public void Balancing_Mode_Solar_Preferred_Load_Maximizes_Grid_Usage()
    {
        // Arrange
        var consumer = new CarChargerEnergyConsumerBuilder(_logger, _testCtx)
            .WithRuntime(TimeSpan.FromMinutes(5), null)
            .InitTeslaTests()
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        _testCtx.TriggerStateChange(consumer.StateSensor, "Occupied");
        _testCtx.TriggerStateChange(consumer.Cars.First().CableConnectedSensor, "on");
        _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "5");
        _testCtx.TriggerStateChange(consumer.Cars.First().MaxBatteryPercentageSensor, "80");
        _testCtx.TriggerStateChange(consumer.Cars.First().Location, "home");
        consumer.BalancingMethod = BalancingMethod.SolarPreferred;

        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Act
        _testCtx.TriggerStateChange(consumer.CurrentEntity, "16");
        _testCtx.TriggerStateChange(consumer.Cars.First().CurrentEntity, "6");
        _testCtx.TriggerStateChange(new Entity(_testCtx.HaContext, "sensor.electricity_meter_power_production_watt"), "800");
        _testCtx.TriggerStateChange(new Entity(_testCtx.HaContext, "sensor.electricity_meter_power_consumption_watt"), "0");

        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

        //Assert
        _testCtx.InputNumberChanged(consumer.Cars.First().CurrentEntity, 7, Moq.Times.Once);
    }

    [TestMethod]
    public void Balancing_Mode_Solar_Preferred_Load_Maximizes_Grid_Usage_But_IsNotJumpy()
    {
        // Arrange
        var consumer = new CarChargerEnergyConsumerBuilder(_logger, _testCtx)
            .WithRuntime(TimeSpan.FromMinutes(5), null)
            .InitTeslaTests()
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        _testCtx.TriggerStateChange(consumer.StateSensor, "Occupied");
        _testCtx.TriggerStateChange(consumer.Cars.First().CableConnectedSensor, "on");
        _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "5");
        _testCtx.TriggerStateChange(consumer.Cars.First().MaxBatteryPercentageSensor, "80");
        _testCtx.TriggerStateChange(consumer.Cars.First().Location, "home");
        consumer.BalancingMethod = BalancingMethod.SolarPreferred;

        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        _testCtx.TriggerStateChange(consumer.CurrentEntity, "16");
        _testCtx.TriggerStateChange(consumer.Cars.First().CurrentEntity, "6");
        _testCtx.TriggerStateChange(new Entity(_testCtx.HaContext, "sensor.electricity_meter_power_production_watt"), "800");
        _testCtx.TriggerStateChange(new Entity(_testCtx.HaContext, "sensor.electricity_meter_power_consumption_watt"), "0");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

        //Act
        _testCtx.TriggerStateChange(consumer.Cars.First().CurrentEntity, "7");
        _testCtx.TriggerStateChange(new Entity(_testCtx.HaContext, "sensor.electricity_meter_power_production_watt"), "100");
        _testCtx.TriggerStateChange(new Entity(_testCtx.HaContext, "sensor.electricity_meter_power_consumption_watt"), "0");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

        //Assert
        _testCtx.InputNumberChanged(consumer.Cars.First().CurrentEntity, 7, Moq.Times.Once);
        _testCtx.InputNumberChanged(consumer.Cars.First().CurrentEntity, 6, Moq.Times.Never);
        _testCtx.InputNumberChanged(consumer.Cars.First().CurrentEntity, 8, Moq.Times.Never);
    }
}