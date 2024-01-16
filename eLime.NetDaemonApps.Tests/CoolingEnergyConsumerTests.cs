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
public class CoolingEnergyConsumerTests
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
        var consumer = new CoolingEnergyConsumerBuilder(_logger, _testCtx)
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        //Act

        //Assert
        Assert.AreEqual("Fridge", energyManager.Consumers.First().Name);
    }

    [TestMethod]
    public void HotFridge_TurnsOn()
    {
        // Arrange
        var consumer = new CoolingEnergyConsumerBuilder(_logger, _testCtx)
            .Build();
        _testCtx.TriggerStateChange(consumer.TemperatureSensor, "4");

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        //Act
        _testCtx.TriggerStateChange(consumer.TemperatureSensor, "7");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Assert
        Assert.AreEqual(EnergyConsumerState.NeedsEnergy, energyManager.Consumers.First().State);
        _testCtx.VerifySwitchTurnOn(consumer.Socket, Moq.Times.Once);
    }

    [TestMethod]
    public void CoolFridge_TurnsOff()
    {
        // Arrange
        var consumer = new CoolingEnergyConsumerBuilder(_logger, _testCtx)
            .Build();
        _testCtx.TriggerStateChange(consumer.TemperatureSensor, "4");

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        //Act
        _testCtx.TriggerStateChange(consumer.TemperatureSensor, "0");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Assert
        Assert.AreEqual(EnergyConsumerState.Off, energyManager.Consumers.First().State);
        _testCtx.VerifySwitchTurnOff(consumer.Socket, Moq.Times.Once);
    }


    [TestMethod]
    public void VeryHotFridge_HasPriorityOver_HotFridge()
    {
        // Arrange

        _testCtx.TriggerStateChange(new Entity(_testCtx.HaContext, "sensor.electricity_meter_power_production_watt"), "100");

        var consumer = new CoolingEnergyConsumerBuilder(_logger, _testCtx)
            .Build();
        var consumer2 = new CoolingEnergyConsumerBuilder(_logger, _testCtx)
            .WithName("VeryHotFridge")
            .WithTemperatureSensor("sensor.very_hot_fridge_temperature", 1, 8)
            .Build();

        _testCtx.TriggerStateChange(consumer.TemperatureSensor, "4");

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .AddConsumer(consumer2)
            .Build();

        //Act
        _testCtx.TriggerStateChange(consumer.TemperatureSensor, "7");
        _testCtx.TriggerStateChange(consumer2.TemperatureSensor, "12");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Assert
        _testCtx.VerifySwitchTurnOn(consumer.Socket, Moq.Times.Never);
        _testCtx.VerifySwitchTurnOn(consumer2.Socket, Moq.Times.Once);
    }

}