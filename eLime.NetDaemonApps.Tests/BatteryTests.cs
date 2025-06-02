using eLime.NetDaemonApps.Domain.EnergyManager;
using eLime.NetDaemonApps.Domain.Storage;
using eLime.NetDaemonApps.Tests.Builders;
using eLime.NetDaemonApps.Tests.Helpers;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Tests;

[TestClass]
public class BatteryTests
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
        A.CallTo(() => _gridMonitor.CurrentLoadMinusBatteries).Returns(-2000);
        A.CallTo(() => _gridMonitor.AverageLoadMinusBatteriesSince(A<DateTimeOffset>._, A<TimeSpan>._)).Returns(-2000);

        _testCtx.TriggerStateChange(new Entity(_testCtx.HaContext, "sensor.voltage"), "230");
    }

    [TestMethod]
    public void Init_HappyFlow()
    {
        // Arrange
        var battery = new BatteryBuilder(_logger, _testCtx)
            .MarstekVenusE()
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler, _gridMonitor)
            .AddBattery(battery)
            .Build();

        //Act

        //Assert
        Assert.AreEqual("Marstek Venus E", energyManager.Batteries.First().Name);
    }

    [TestMethod]
    public void Disables_Discharging_If_Consumer_Is_Running_And_Does_Not_Allow_Battery_power()
    {
        // Arrange
        var consumer = new CarChargerEnergyConsumerBuilder(_logger, _testCtx)
            .WithRuntime(TimeSpan.FromMinutes(5), null)
            .InitTeslaTests()
            .Build();

        var battery = new BatteryBuilder(_logger, _testCtx)
            .MarstekVenusE()
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler, _gridMonitor)
            .AddConsumer(consumer)
            .AddBattery(battery)
            .Build();

        _testCtx.TriggerStateChange(consumer.StateSensor, "Occupied");
        _testCtx.TriggerStateChange(consumer.Cars.First().ChargingStateSensor, "no_power");
        _testCtx.TriggerStateChange(consumer.Cars.First().CableConnectedSensor, "on");
        _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "5");
        _testCtx.TriggerStateChange(consumer.Cars.First().MaxBatteryPercentageSensor, "80");
        _testCtx.TriggerStateChange(consumer.Cars.First().Location, "home");
        _testCtx.TriggerStateChange(battery.MaxDischargePowerEntity, "800");

        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));
        consumer.SetBalancingMethod(_testCtx.Scheduler.Now, BalancingMethod.SolarOnly);

        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        _testCtx.TriggerStateChange(consumer.StateSensor, "Charging");
        _testCtx.TriggerStateChange(consumer.CurrentEntity, "16");
        _testCtx.TriggerStateChange(consumer.Cars.First().ChargingStateSensor, "charging");
        _testCtx.TriggerStateChange(consumer.Cars.First().CurrentEntity, "6");
        _testCtx.TriggerStateChange(consumer.Cars.First().ChargerSwitch, "on");
        _testCtx.TriggerStateChange(consumer.PowerUsage, "4000");

        //Act
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(10));
        A.CallTo(() => _gridMonitor.CurrentLoad).Returns(0);
        A.CallTo(() => _gridMonitor.AverageLoadSince(A<DateTimeOffset>._, A<TimeSpan>._)).Returns(0);
        A.CallTo(() => _gridMonitor.CurrentLoadMinusBatteries).Returns(-1800);
        A.CallTo(() => _gridMonitor.AverageLoadMinusBatteriesSince(A<DateTimeOffset>._, A<TimeSpan>._)).Returns(-1800);
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(25));

        //Assert
        _testCtx.NumberChanged(battery.MaxDischargePowerEntity, 0, Moq.Times.Once);
    }

    [TestMethod]
    public void Enables_Discharging_If_Consumer_Is_Running_And_Does_Allow_Battery_power()
    {
        // Arrange
        var consumer = new CarChargerEnergyConsumerBuilder(_logger, _testCtx)
            .WithRuntime(TimeSpan.FromMinutes(5), null)
            .InitTeslaTests()
            .Build();

        var battery = new BatteryBuilder(_logger, _testCtx)
            .MarstekVenusE()
            .Build();

        var energyManager = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler, _gridMonitor)
            .AddConsumer(consumer)
            .AddBattery(battery)
            .Build();

        _testCtx.TriggerStateChange(consumer.StateSensor, "Occupied");
        _testCtx.TriggerStateChange(consumer.Cars.First().ChargingStateSensor, "no_power");
        _testCtx.TriggerStateChange(consumer.Cars.First().CableConnectedSensor, "on");
        _testCtx.TriggerStateChange(consumer.Cars.First().BatteryPercentageSensor, "5");
        _testCtx.TriggerStateChange(consumer.Cars.First().MaxBatteryPercentageSensor, "80");
        _testCtx.TriggerStateChange(consumer.Cars.First().Location, "home");
        _testCtx.TriggerStateChange(battery.MaxDischargePowerEntity, "0");

        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));
        consumer.SetBalancingMethod(_testCtx.Scheduler.Now, BalancingMethod.SolarOnly);
        consumer.SetAllowBatteryPower(AllowBatteryPower.Yes);

        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        _testCtx.TriggerStateChange(consumer.StateSensor, "Charging");
        _testCtx.TriggerStateChange(consumer.CurrentEntity, "16");
        _testCtx.TriggerStateChange(consumer.Cars.First().ChargingStateSensor, "charging");
        _testCtx.TriggerStateChange(consumer.Cars.First().CurrentEntity, "6");
        _testCtx.TriggerStateChange(consumer.Cars.First().ChargerSwitch, "on");
        _testCtx.TriggerStateChange(consumer.PowerUsage, "4000");

        //Act
        A.CallTo(() => _gridMonitor.CurrentLoad).Returns(0);
        A.CallTo(() => _gridMonitor.AverageLoadSince(A<DateTimeOffset>._, A<TimeSpan>._)).Returns(0);
        A.CallTo(() => _gridMonitor.CurrentLoadMinusBatteries).Returns(-1800);
        A.CallTo(() => _gridMonitor.AverageLoadMinusBatteriesSince(A<DateTimeOffset>._, A<TimeSpan>._)).Returns(-1800);
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(10));

        //Assert
        _testCtx.NumberChanged(battery.MaxDischargePowerEntity, 800, Moq.Times.Once);
    }
}