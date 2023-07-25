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
public class CarChargerEnergyConsumerTests
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
        var consumer = new CarChargerEnergyConsumerBuilder(_testCtx)
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
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
        var consumer = new CarChargerEnergyConsumerBuilder(_testCtx)
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        //Act
        _testCtx.TriggerStateChange(consumer.StateSensor, "Occupied");
        _testCtx.TriggerStateChange(consumer.Cars.First().CableConnectedSensor, "on");
        _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "5");

        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Assert
        Assert.AreEqual(EnergyConsumerState.NeedsEnergy, energyManager.Consumers.First().State);
        _testCtx.InputNumberChanged(consumer.CurrentEntity, 6, Moq.Times.Once);
    }

    [TestMethod]
    public void Occupied__ButNoKnownCar_DoesNotTrigger_TurnsOn()
    {
        // Arrange
        var consumer = new CarChargerEnergyConsumerBuilder(_testCtx)
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
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
    public void ExcessEnergy_Adjusts_Load()
    {
        // Arrange
        var consumer = new CarChargerEnergyConsumerBuilder(_testCtx)
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();


        _testCtx.TriggerStateChange(consumer.StateSensor, "Occupied");
        _testCtx.TriggerStateChange(consumer.Cars.First().CableConnectedSensor, "on");
        _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "5");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Act
        _testCtx.TriggerStateChange(consumer.CurrentEntity, "6");
        _testCtx.TriggerStateChange(new Entity(_testCtx.HaContext, "sensor.electricity_meter_power_production_watt"), "600");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

        //Assert
        Assert.AreEqual(EnergyConsumerState.Running, energyManager.Consumers.First().State);
        _testCtx.InputNumberChanged(consumer.CurrentEntity, 8, Moq.Times.Once);
    }

    [TestMethod]
    public void ConsumingEnergy_Adjusts_Load()
    {
        // Arrange
        var consumer = new CarChargerEnergyConsumerBuilder(_testCtx)
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();


        _testCtx.TriggerStateChange(consumer.StateSensor, "Occupied");
        _testCtx.TriggerStateChange(consumer.Cars.First().CableConnectedSensor, "on");
        _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "5");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Act
        _testCtx.TriggerStateChange(consumer.CurrentEntity, "16");
        _testCtx.TriggerStateChange(new Entity(_testCtx.HaContext, "sensor.electricity_meter_power_production_watt"), "0");
        _testCtx.TriggerStateChange(new Entity(_testCtx.HaContext, "sensor.electricity_meter_power_consumption_watt"), "800");

        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

        //Assert
        Assert.AreEqual(EnergyConsumerState.Running, energyManager.Consumers.First().State);
        _testCtx.InputNumberChanged(consumer.CurrentEntity, 13, Moq.Times.Once);
    }

    [TestMethod]
    public void ConsumingEnergy_IsNotJumpy()
    {
        // Arrange
        var consumer = new CarChargerEnergyConsumerBuilder(_testCtx)
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();


        _testCtx.TriggerStateChange(consumer.StateSensor, "Occupied");
        _testCtx.TriggerStateChange(consumer.Cars.First().CableConnectedSensor, "on");
        _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "5");
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
        Assert.AreEqual(EnergyConsumerState.Running, energyManager.Consumers.First().State);
        _testCtx.InputNumberChanged(consumer.CurrentEntity, 13, Moq.Times.Once);
        _testCtx.InputNumberChanged(consumer.CurrentEntity, 12, Moq.Times.Never);
        _testCtx.InputNumberChanged(consumer.CurrentEntity, 14, Moq.Times.Never);
    }

    [TestMethod]
    public void Charged_Triggers_TurnOff()
    {
        // Arrange
        var consumer = new CarChargerEnergyConsumerBuilder(_testCtx)
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        //Act
        _testCtx.TriggerStateChange(consumer.StateSensor, "Occupied");
        _testCtx.TriggerStateChange(consumer.Cars.First().CableConnectedSensor, "on");
        _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "5");

        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        _testCtx.TriggerStateChange(consumer.StateSensor, "Charging");
        _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "100");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

        //Assert
        Assert.AreEqual(EnergyConsumerState.Off, energyManager.Consumers.First().State);
        _testCtx.InputNumberChanged(consumer.CurrentEntity, 5, Moq.Times.Exactly(2));
    }
}