using eLime.NetDaemonApps.Config.EnergyManager;
using eLime.NetDaemonApps.Domain.EnergyManager.BatteryManager;
using eLime.NetDaemonApps.Domain.EnergyManager.Consumers;
using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Domain.Storage;
using eLime.NetDaemonApps.Tests.EnergyManager.Builders;
using eLime.NetDaemonApps.Tests.Helpers;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using System.Globalization;
using AllowBatteryPower = eLime.NetDaemonApps.Domain.EnergyManager.Consumers.DynamicConsumers.AllowBatteryPower;
using BalancingMethod = eLime.NetDaemonApps.Domain.EnergyManager.Consumers.DynamicConsumers.BalancingMethod;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace eLime.NetDaemonApps.Tests.EnergyManager;

[TestClass]
public class BatteryTests
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

    private void InitChargerState(EnergyConsumerConfig consumer, string state, int voltage, bool cableConnected, int batteryPercentage, string location, int? current = null, int? powerConsumption = null)
    {
        _testCtx.TriggerStateChange(consumer.CarCharger!.StateSensor, state);
        _testCtx.TriggerStateChange(consumer.CarCharger!.VoltageEntity, voltage.ToString());
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().CableConnectedSensor, cableConnected ? "on" : "off");
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().BatteryPercentageSensor, batteryPercentage.ToString());
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().Location, location);
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        if (current != null)
            _testCtx.TriggerStateChange(consumer.CarCharger!.CurrentEntity, current.Value.ToString());

        if (powerConsumption != null)
            _testCtx.TriggerStateChange(consumer.PowerUsageEntity, powerConsumption.Value.ToString());
    }
    [TestMethod]
    public async Task Init_HappyFlow()
    {
        // Arrange
        var battery = new BatteryBuilder()
            .MarstekVenusE()
            .Build();

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddBattery(battery);
        var energyManager = await builder.Build();

        //Act

        //Assert
        Assert.AreEqual("Marstek Venus E", energyManager.BatteryManager.Batteries.First().Name);
    }

    [TestMethod]
    public async Task Disables_Discharging_If_Consumer_Is_Running_And_Does_Not_Allow_Battery_power()
    {
        // Arrange
        A.CallTo(() => _fileStorage.Get<ConsumerState>("EnergyManager", "veton")).Returns(new ConsumerState
        {
            BalancingMethod = BalancingMethod.SolarOnly,
            AllowBatteryPower = AllowBatteryPower.No
        });

        var consumer = CarChargerEnergyConsumerBuilder.Tesla().Build();
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().MaxBatteryPercentageSensor!, "80");

        var battery = new BatteryBuilder()
            .MarstekVenusE()
            .Build();

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .AddBattery(battery);
        _ = await builder.Build();

        InitChargerState(consumer, "Charging", 230, true, 50, "home", 5, 4200);
        _testCtx.TriggerStateChange(consumer.CarCharger!.CurrentEntity, "16");
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().ChargerSwitch!, "on");
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().CurrentEntity!, "6");
        _testCtx.TriggerStateChange(battery.MaxDischargePowerEntity, "800");

        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(5));

        //Act
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "0");
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "0");
        _testCtx.TriggerStateChange(builder._batteryManager.TotalDischargePowerSensor, "800");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(25));

        //Assert
        _testCtx.NumberChanged(battery.MaxDischargePowerEntity, 0, Moq.Times.Once);
    }

    [TestMethod]
    public async Task Enables_Discharging_If_Consumer_Is_Running_And_Does_Allow_Battery_power()
    {
        // Arrange
        A.CallTo(() => _fileStorage.Get<ConsumerState>("EnergyManager", "veton")).Returns(new ConsumerState
        {
            BalancingMethod = BalancingMethod.SolarOnly,
            AllowBatteryPower = AllowBatteryPower.Yes
        });

        var consumer = CarChargerEnergyConsumerBuilder.Tesla().Build();
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().MaxBatteryPercentageSensor!, "80");

        var battery = new BatteryBuilder()
            .MarstekVenusE()
            .Build();

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .AddBattery(battery);
        _ = await builder.Build();

        InitChargerState(consumer, "Charging", 230, true, 50, "home", 5, 4200);
        _testCtx.TriggerStateChange(consumer.CarCharger!.CurrentEntity, "16");
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().ChargerSwitch!, "on");
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().CurrentEntity!, "6");
        _testCtx.TriggerStateChange(battery.MaxDischargePowerEntity, "800");

        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(5));

        //Act
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "0");
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "0");
        _testCtx.TriggerStateChange(builder._batteryManager.TotalDischargePowerSensor, "0");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(25));

        //Assert
        _testCtx.NumberChanged(battery.MaxDischargePowerEntity, 800, Moq.Times.Once);
    }

    [TestMethod]
    public async Task CalculatesRoundTripEfficiency()
    {
        // Arrange
        var battery = new BatteryBuilder()
            .MarstekVenusE()
            .Build();
        _testCtx.TriggerStateChange(battery.MaxChargePowerEntity, "800");
        _testCtx.TriggerStateChange(battery.MaxDischargePowerEntity, "800");

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddBattery(battery);
        var energyManager = await builder.Build();
        _testCtx.TriggerStateChange(battery.TotalEnergyChargedSensor, "5.3");
        _testCtx.TriggerStateChange(battery.TotalEnergyDischargedSensor, "4.3");

        //Act
        _testCtx.TriggerStateChange(battery.StateOfChargeSensor, "50");

        //Assert
        Assert.AreEqual(81.13, energyManager.BatteryManager.Batteries.First().State.RoundTripEfficiency);
    }

    [TestMethod]
    public async Task Saves_State()
    {
        // Arrange
        A.CallTo(() => _fileStorage.Get<ConsumerState>("EnergyManager", "veton")).Returns(new ConsumerState
        {
            BalancingMethod = BalancingMethod.SolarOnly,
            AllowBatteryPower = AllowBatteryPower.Yes
        });
        A.CallTo(() => _fileStorage.Get<BatteryState>("EnergyManager", "marstek_venus_e")).Returns(new BatteryState
        {
            RoundTripEfficiency = 78.5d
        });

        var consumer = CarChargerEnergyConsumerBuilder.Tesla().Build();
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().MaxBatteryPercentageSensor!, "80");

        var battery = new BatteryBuilder()
            .MarstekVenusE()
            .Build();
        _testCtx.TriggerStateChange(battery.MaxDischargePowerEntity, "800");

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .AddBattery(battery);
        var energyManager = await builder.Build();

        InitChargerState(consumer, "Charging", 230, true, 50, "home", 5, 4200);
        _testCtx.TriggerStateChange(consumer.CarCharger!.CurrentEntity, "16");
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().ChargerSwitch!, "on");
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().CurrentEntity!, "6");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(5));

        //Act
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "0");
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "0");
        _testCtx.TriggerStateChange(builder._batteryManager.TotalDischargePowerSensor, "0");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(25));

        //Assert
        var mqttRte = $"sensor.battery_{battery.Name.MakeHaFriendly()}_rte";
        A.CallTo(() => _mqttEntityManager.SetStateAsync(mqttRte, energyManager.BatteryManager.Batteries.First().State.RoundTripEfficiency.ToString("N", new NumberFormatInfo { NumberDecimalSeparator = "." }))).MustHaveHappenedOnceExactly();
        A.CallTo(() => _fileStorage.Save("EnergyManager", "marstek_venus_e", A<BatteryState>._)).MustHaveHappened();
    }
}