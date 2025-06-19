using eLime.NetDaemonApps.Config.EnergyManager;
using eLime.NetDaemonApps.Domain.EnergyManager;
using eLime.NetDaemonApps.Domain.EnergyManager2.Consumers;
using eLime.NetDaemonApps.Domain.EnergyManager2.Consumers.DynamicConsumers.CarCharger;
using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Domain.Storage;
using eLime.NetDaemonApps.Tests.Builders;
using eLime.NetDaemonApps.Tests.Helpers;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using System.Reactive.Subjects;
using AllowBatteryPower = eLime.NetDaemonApps.Domain.EnergyManager2.Consumers.DynamicConsumers.AllowBatteryPower;
using BalancingMethod = eLime.NetDaemonApps.Domain.EnergyManager2.Consumers.DynamicConsumers.BalancingMethod;
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
    public async Task Init()
    {
        _testCtx = AppTestContext.Create(DateTime.Now);

        _logger = A.Fake<ILogger<EnergyManager>>();
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
        InitChargerState(consumer, "Occupied", 230, true, 5, "home");

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
        InitChargerState(consumer, "Occupied", 230, false, 5, "home");

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
        InitChargerState(consumer, "Occupied", 230, false, 5, "away");

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
        InitChargerState(consumer, "Occupied", 230, true, 5, "home");

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

        InitChargerState(consumer, "Charging", 230, true, 5, "home", 6, 1400);

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

        InitChargerState(consumer, "Charging", 230, true, 5, "home", 16, 3600);

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
        InitChargerState(consumer, "Charging", 230, true, 5, "home", 6, 1400);

        //Act
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "1200");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

        //Assert
        Assert.AreEqual(EnergyConsumerState.Running, energyManager.Consumers.First().State.State);
        _testCtx.InputNumberChanged(consumer.CarCharger!.CurrentEntity, 6, Moq.Times.Once);
        _testCtx.InputNumberChanged(consumer.CarCharger!.CurrentEntity, 5, Moq.Times.Never);
    }


    [TestMethod]
    public async Task ConsumingEnergy_ShutsDown_After_MinimumRuntime()
    {
        // Arrange
        var consumer = CarChargerEnergyConsumer2Builder.Passat()
            .WithRuntime(TimeSpan.FromMinutes(5), null)
            .Build();

        var builder = new EnergyManager2Builder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        _ = await builder.Build();

        InitChargerState(consumer, "Charging", 230, true, 5, "home", 6, 1400);

        //Act
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "1200");
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(6));

        //Assert
        _testCtx.InputNumberChanged(consumer.CarCharger!.CurrentEntity, 5, Moq.Times.Once);
    }

    [TestMethod]
    public async Task ConsumingEnergy_IsNotJumpy()
    {
        // Arrange
        var consumer = CarChargerEnergyConsumer2Builder.Passat()
            .Build();

        var builder = new EnergyManager2Builder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        _ = await builder.Build();

        InitChargerState(consumer, "Charging", 230, true, 5, "home", 16, 3600);
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "0");
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "800");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

        //Act
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "110");
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "0");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

        //Assert
        _testCtx.InputNumberChanged(consumer.CarCharger!.CurrentEntity, 12, Moq.Times.Once);
        _testCtx.InputNumberChanged(consumer.CarCharger!.CurrentEntity, 11, Moq.Times.Never);
        _testCtx.InputNumberChanged(consumer.CarCharger!.CurrentEntity, 13, Moq.Times.Never);
    }

    [TestMethod]
    public async Task Charged_Triggers_TurnOff()
    {
        // Arrange
        var consumer = CarChargerEnergyConsumer2Builder.Passat()
            .Build();

        var builder = new EnergyManager2Builder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        var energyManager = await builder.Build();

        //Act
        InitChargerState(consumer, "Charging", 230, true, 100, "home", 16, 3600);

        //Assert
        Assert.AreEqual(EnergyConsumerState.Off, energyManager.Consumers.First().State.State);
        _testCtx.InputNumberChanged(consumer.CarCharger!.CurrentEntity, 5, Moq.Times.Once);
    }

    [TestMethod]
    public async Task MaxBatteryReached_Triggers_TurnOff()
    {
        // Arrange
        var consumer = CarChargerEnergyConsumer2Builder.Tesla()
            .Build();
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().MaxBatteryPercentageSensor!, "80");

        var builder = new EnergyManager2Builder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        var energyManager = await builder.Build();

        InitChargerState(consumer, "Charging", 230, true, 50, "home", 16, 700);
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().ChargerSwitch!, "on");
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().CurrentEntity!, "1");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(5));

        //Act
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().BatteryPercentageSensor, "80");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(5));

        //Assert
        Assert.AreEqual(EnergyConsumerState.Off, energyManager.Consumers.First().State.State);
        _testCtx.VerifySwitchTurnOff(consumer.CarCharger!.Cars.First().ChargerSwitch!, Moq.Times.Once);
    }


    [TestMethod]
    public async Task MaxBatteryReached_DoesNotTriggerTurnOff_IfRemainOnAtFullBattery()
    {
        // Arrange
        var consumer = CarChargerEnergyConsumer2Builder.Tesla(true)
            .Build();
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().MaxBatteryPercentageSensor!, "80");

        var builder = new EnergyManager2Builder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        var energyManager = await builder.Build();

        InitChargerState(consumer, "Charging", 230, true, 50, "home", 16, 700);
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().ChargerSwitch!, "on");
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().CurrentEntity!, "1");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(5));

        //Act
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().BatteryPercentageSensor, "80");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(5));

        //Assert
        Assert.AreEqual(EnergyConsumerState.Running, energyManager.Consumers.First().State.State);
        _testCtx.VerifySwitchTurnOff(consumer.CarCharger!.Cars.First().ChargerSwitch!, Moq.Times.Never);
    }

    [TestMethod]
    public async Task ExcessEnergy_Adjusts_Load_3Phase()
    {
        // Arrange
        var consumer = CarChargerEnergyConsumer2Builder.Tesla()
            .Build();
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().MaxBatteryPercentageSensor!, "80");

        var builder = new EnergyManager2Builder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        _ = await builder.Build();

        InitChargerState(consumer, "Charging", 230, true, 50, "home", 6, 4000);
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().ChargerSwitch!, "on");
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().CurrentEntity!, "6");
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "900");
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "0");

        //Act
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(20));

        //Assert
        _testCtx.InputNumberChanged(consumer.CarCharger!.CurrentEntity, 16, Moq.Times.Once);
        _testCtx.InputNumberChanged(consumer.CarCharger!.Cars.First().CurrentEntity!, 7, Moq.Times.Once);
    }

    [TestMethod]
    public async Task Occupied_Triggers_TurnOn_AndAdjustsCarCurrentForSupportedCar()
    {
        // Arrange
        var consumer = CarChargerEnergyConsumer2Builder.Tesla()
            .Build();
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().MaxBatteryPercentageSensor!, "80");

        var builder = new EnergyManager2Builder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        var energyManager = await builder.Build();
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "1000");
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "0");

        //Act
        InitChargerState(consumer, "Occupied", 230, true, 50, "home", 5, 0);

        //Assert
        Assert.AreEqual(EnergyConsumerState.NeedsEnergy, energyManager.Consumers.First().State.State);
        _testCtx.InputNumberChanged(consumer.CarCharger!.CurrentEntity, 16, Moq.Times.Once);

        _testCtx.VerifySwitchTurnOn(consumer.CarCharger!.Cars.First().ChargerSwitch!, Moq.Times.Once);
        _testCtx.InputNumberChanged(consumer.CarCharger!.Cars.First().CurrentEntity!, 1, Moq.Times.Once);
    }

    [TestMethod]
    public async Task ExcessEnergy_Adjusts_Load_AndAdjustsCarCurrentForSupportedCar()
    {
        // Arrange
        var consumer = CarChargerEnergyConsumer2Builder.Tesla()
            .Build();
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().MaxBatteryPercentageSensor!, "80");

        var builder = new EnergyManager2Builder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        _ = await builder.Build();

        InitChargerState(consumer, "Charging", 230, true, 50, "home", 5, 700);
        _testCtx.TriggerStateChange(consumer.CarCharger!.CurrentEntity, "16");
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().ChargerSwitch!, "on");
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().CurrentEntity!, "1");

        //Act
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "1000");
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "0");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(5));

        //Assert
        _testCtx.InputNumberChanged(consumer.CarCharger!.Cars.First().CurrentEntity!, 2, Moq.Times.Once);
    }

    [TestMethod]
    public async Task ExcessEnergy_Adjusts_ChargerAndCarCurrentForSupportedCar()
    {
        // Arrange
        var consumer = CarChargerEnergyConsumer2Builder.Tesla()
            .Build();
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().MaxBatteryPercentageSensor!, "80");

        var builder = new EnergyManager2Builder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        _ = await builder.Build();

        InitChargerState(consumer, "Charging", 230, true, 50, "home", 5, 700);
        _testCtx.TriggerStateChange(consumer.CarCharger!.CurrentEntity, "6");
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().ChargerSwitch!, "on");
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().CurrentEntity!, "1");

        //Act
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "3500");
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "0");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(5));

        //Assert
        _testCtx.InputNumberChanged(consumer.CarCharger!.CurrentEntity, 16, Moq.Times.Once);
        _testCtx.InputNumberChanged(consumer.CarCharger!.Cars.First().CurrentEntity!, 6, Moq.Times.Once);
    }

    [TestMethod]
    public async Task ConsumingEnergy_Adjusts_OnlyCarWhenBelowChargerMinimumCurrent()
    {
        // Arrange
        var consumer = CarChargerEnergyConsumer2Builder.Tesla()
            .Build();
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().MaxBatteryPercentageSensor!, "80");

        var builder = new EnergyManager2Builder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        _ = await builder.Build();

        InitChargerState(consumer, "Charging", 230, true, 50, "home", 5, 4200);
        _testCtx.TriggerStateChange(consumer.CarCharger!.CurrentEntity, "16");
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().ChargerSwitch!, "on");
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().CurrentEntity!, "6");

        //Act
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "0");
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "900");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(5));

        //Assert
        _testCtx.InputNumberChanged(consumer.CarCharger!.Cars.First().CurrentEntity!, 4, Moq.Times.Once);
    }

    [TestMethod]
    public async Task Balancing_Mode_Near_Peak_Load_Maximizes_Grid_Usage()
    {
        // Arrange
        A.CallTo(() => _fileStorage.Get<ConsumerState>("EnergyManager", "Veton")).Returns(new ConsumerState
        {
            BalancingMethod = BalancingMethod.NearPeak
        });

        var consumer = CarChargerEnergyConsumer2Builder.Tesla()
            .Build();
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().MaxBatteryPercentageSensor!, "80");

        var builder = new EnergyManager2Builder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        _ = await builder.Build();

        InitChargerState(consumer, "Charging", 230, true, 50, "home", 5, 4200);
        _testCtx.TriggerStateChange(consumer.CarCharger!.CurrentEntity, "16");
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().ChargerSwitch!, "on");
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().CurrentEntity!, "6");

        //Act
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "0");
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "0");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

        //Assert
        _testCtx.InputNumberChanged(consumer.CarCharger!.Cars.First().CurrentEntity!, 9, Moq.Times.Once);
    }

    [TestMethod]
    public async Task Balancing_Mode_Solar_Preferred_Load_Maximizes_Grid_Usage()
    {
        A.CallTo(() => _fileStorage.Get<ConsumerState>("EnergyManager", "Veton")).Returns(new ConsumerState
        {
            BalancingMethod = BalancingMethod.SolarPreferred
        });

        var consumer = CarChargerEnergyConsumer2Builder.Tesla()
            .Build();
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().MaxBatteryPercentageSensor!, "80");

        var builder = new EnergyManager2Builder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        _ = await builder.Build();

        InitChargerState(consumer, "Charging", 230, true, 50, "home", 5, 4200);
        _testCtx.TriggerStateChange(consumer.CarCharger!.CurrentEntity, "16");
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().ChargerSwitch!, "on");
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().CurrentEntity!, "6");

        //Act
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "900");
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "0");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

        //Assert
        _testCtx.InputNumberChanged(consumer.CarCharger!.Cars.First().CurrentEntity!, 8, Moq.Times.Once);
    }


    [TestMethod]
    public async Task Balancing_Mode_Solar_Only_Load_Minimizes_Grid_Usage()
    {
        A.CallTo(() => _fileStorage.Get<ConsumerState>("EnergyManager", "Veton")).Returns(new ConsumerState
        {
            BalancingMethod = BalancingMethod.SolarOnly
        });

        var consumer = CarChargerEnergyConsumer2Builder.Tesla()
            .Build();
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().MaxBatteryPercentageSensor!, "80");

        var builder = new EnergyManager2Builder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        _ = await builder.Build();

        InitChargerState(consumer, "Charging", 230, true, 50, "home", 5, 4200);
        _testCtx.TriggerStateChange(consumer.CarCharger!.CurrentEntity, "16");
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().ChargerSwitch!, "on");
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().CurrentEntity!, "6");

        //Act
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "900");
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "0");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

        //Assert
        _testCtx.InputNumberChanged(consumer.CarCharger!.Cars.First().CurrentEntity!, 7, Moq.Times.Once);
    }

    [TestMethod]
    public async Task Balancing_Mode_Solar_Surplus_Load_Minimizes_Grid_Usage()
    {
        A.CallTo(() => _fileStorage.Get<ConsumerState>("EnergyManager", "Veton")).Returns(new ConsumerState
        {
            BalancingMethod = BalancingMethod.SolarSurplus
        });

        var consumer = CarChargerEnergyConsumer2Builder.Tesla()
            .Build();
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().MaxBatteryPercentageSensor!, "80");

        var builder = new EnergyManager2Builder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        _ = await builder.Build();

        InitChargerState(consumer, "Charging", 230, true, 50, "home", 5, 4200);
        _testCtx.TriggerStateChange(consumer.CarCharger!.CurrentEntity, "16");
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().ChargerSwitch!, "on");
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().CurrentEntity!, "6");

        //Act
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "1800");
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "0");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

        //Assert
        _testCtx.InputNumberChanged(consumer.CarCharger!.Cars.First().CurrentEntity!, 7, Moq.Times.Once);
    }

    [TestMethod]
    public async Task Balancing_Mode_Solar_Preferred_Load_Maximizes_Grid_Usage_But_IsNotJumpy()
    {
        A.CallTo(() => _fileStorage.Get<ConsumerState>("EnergyManager", "Veton")).Returns(new ConsumerState
        {
            BalancingMethod = BalancingMethod.SolarPreferred
        });

        var consumer = CarChargerEnergyConsumer2Builder.Tesla()
            .Build();
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().MaxBatteryPercentageSensor!, "80");

        var builder = new EnergyManager2Builder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        _ = await builder.Build();

        InitChargerState(consumer, "Charging", 230, true, 50, "home", 5, 4200);
        _testCtx.TriggerStateChange(consumer.CarCharger!.CurrentEntity, "16");
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().ChargerSwitch!, "on");
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().CurrentEntity!, "6");

        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "900");
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "0");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().CurrentEntity!, "8");
        _testCtx.TriggerStateChange(consumer.PowerUsageEntity, "5600");

        //Act
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "200");
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "0");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

        //Assert
        _testCtx.InputNumberChanged(consumer.CarCharger!.Cars.First().CurrentEntity!, 8, Moq.Times.Once);
        _testCtx.InputNumberChanged(consumer.CarCharger!.Cars.First().CurrentEntity!, 7, Moq.Times.Never);
        _testCtx.InputNumberChanged(consumer.CarCharger!.Cars.First().CurrentEntity!, 9, Moq.Times.Never);
    }

    [TestMethod]
    public async Task Battery_ChargePower_Is_Included()
    {
        A.CallTo(() => _fileStorage.Get<ConsumerState>("EnergyManager", "Veton")).Returns(new ConsumerState
        {
            BalancingMethod = BalancingMethod.SolarOnly,
            AllowBatteryPower = AllowBatteryPower.Yes
        });

        var consumer = CarChargerEnergyConsumer2Builder.Tesla()
            .Build();
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().MaxBatteryPercentageSensor!, "80");

        var builder = new EnergyManager2Builder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        _ = await builder.Build();

        InitChargerState(consumer, "Charging", 230, true, 50, "home", 5, 4200);
        _testCtx.TriggerStateChange(consumer.CarCharger!.CurrentEntity, "16");
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().ChargerSwitch!, "on");
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().CurrentEntity!, "6");

        //Act
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "0");
        _testCtx.TriggerStateChange(builder._batteryManager.TotalChargePowerSensor, "1800");
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "0");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

        //Assert
        _testCtx.InputNumberChanged(consumer.CarCharger!.Cars.First().CurrentEntity!, 8, Moq.Times.Never);
    }

    [TestMethod]
    public async Task Battery_ChargePower_Is_ExcludedIfNotAllowed()
    {
        A.CallTo(() => _fileStorage.Get<ConsumerState>("EnergyManager", "Veton")).Returns(new ConsumerState
        {
            BalancingMethod = BalancingMethod.SolarOnly,
            AllowBatteryPower = AllowBatteryPower.No
        });

        var consumer = CarChargerEnergyConsumer2Builder.Tesla()
            .Build();
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().MaxBatteryPercentageSensor!, "80");

        var builder = new EnergyManager2Builder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        _ = await builder.Build();

        InitChargerState(consumer, "Charging", 230, true, 50, "home", 5, 4200);
        _testCtx.TriggerStateChange(consumer.CarCharger!.CurrentEntity, "16");
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().ChargerSwitch!, "on");
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().CurrentEntity!, "6");

        //Act
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "0");
        _testCtx.TriggerStateChange(builder._batteryManager.TotalChargePowerSensor, "1800");
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "0");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

        //Assert
        _testCtx.InputNumberChanged(consumer.CarCharger!.Cars.First().CurrentEntity!, 8, Moq.Times.Once);
    }

    [TestMethod]
    public async Task Battery_DischargePower_Is_Included()
    {
        A.CallTo(() => _fileStorage.Get<ConsumerState>("EnergyManager", "Veton")).Returns(new ConsumerState
        {
            BalancingMethod = BalancingMethod.SolarOnly,
            AllowBatteryPower = AllowBatteryPower.Yes
        });

        var consumer = CarChargerEnergyConsumer2Builder.Tesla()
            .Build();
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().MaxBatteryPercentageSensor!, "80");

        var builder = new EnergyManager2Builder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        _ = await builder.Build();

        InitChargerState(consumer, "Charging", 230, true, 50, "home", 5, 4200);
        _testCtx.TriggerStateChange(consumer.CarCharger!.CurrentEntity, "16");
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().ChargerSwitch!, "on");
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().CurrentEntity!, "6");

        //Act
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "0");
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "0");
        _testCtx.TriggerStateChange(builder._batteryManager.TotalDischargePowerSensor, "1800");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

        //Assert
        _testCtx.InputNumberChanged(consumer.CarCharger!.Cars.First().CurrentEntity!, 8, Moq.Times.Never);
    }

    [TestMethod]
    public async Task Battery_DischargePower_Is_Excluded_If_Not_Allowed()
    {
        A.CallTo(() => _fileStorage.Get<ConsumerState>("EnergyManager", "Veton")).Returns(new ConsumerState
        {
            BalancingMethod = BalancingMethod.SolarOnly,
            AllowBatteryPower = AllowBatteryPower.No
        });

        var consumer = CarChargerEnergyConsumer2Builder.Tesla()
            .Build();
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().MaxBatteryPercentageSensor!, "80");

        var builder = new EnergyManager2Builder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        _ = await builder.Build();

        InitChargerState(consumer, "Charging", 230, true, 50, "home", 5, 4200);
        _testCtx.TriggerStateChange(consumer.CarCharger!.CurrentEntity, "16");
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().ChargerSwitch!, "on");
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().CurrentEntity!, "6");

        //Act
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "0");
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "0");
        _testCtx.TriggerStateChange(builder._batteryManager.TotalDischargePowerSensor, "1800");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

        //Assert
        _testCtx.InputNumberChanged(consumer.CarCharger!.Cars.First().CurrentEntity!, 3, Moq.Times.Once);
    }

    [TestMethod]
    public async Task Saves_State()
    {
        A.CallTo(() => _fileStorage.Get<ConsumerState>("EnergyManager", "Veton")).Returns(new ConsumerState
        {
            BalancingMethod = BalancingMethod.SolarOnly,
            AllowBatteryPower = AllowBatteryPower.Yes
        });

        var consumer = CarChargerEnergyConsumer2Builder.Tesla()
            .Build();
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().MaxBatteryPercentageSensor!, "80");

        var builder = new EnergyManager2Builder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        _ = await builder.Build();
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "1000");
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "0");

        //Act
        InitChargerState(consumer, "Occupied", 230, true, 50, "home", 5, 0);

        //Assert
        var mqttState = $"sensor.energy_consumer_{consumer.Name.MakeHaFriendly()}_state";
        var mqttBalancingMethod = $"sensor.energy_consumer_{consumer.Name.MakeHaFriendly()}_balancing_method";
        var mqttAllowBatteryPower = $"sensor.energy_consumer_{consumer.Name.MakeHaFriendly()}_allow_battery_power";
        A.CallTo(() => _mqttEntityManager.SetStateAsync(mqttState, EnergyConsumerState.NeedsEnergy.ToString())).MustHaveHappenedOnceExactly();
        A.CallTo(() => _mqttEntityManager.SetStateAsync(mqttBalancingMethod, BalancingMethod.SolarOnly.ToString())).MustHaveHappened();
        A.CallTo(() => _mqttEntityManager.SetStateAsync(mqttAllowBatteryPower, AllowBatteryPower.Yes.ToString())).MustHaveHappened();
        A.CallTo(() => _fileStorage.Save("EnergyManager", "Veton", A<ConsumerState>._)).MustHaveHappened();
    }

    [TestMethod]
    public async Task Listens_To_Mqtt_Events()
    {
        var mqttBalancingMethod = $"sensor.energy_consumer_veton_balancing_method";
        A.CallTo(() => _fileStorage.Get<ConsumerState>("EnergyManager", "Veton")).Returns(new ConsumerState
        {
            BalancingMethod = BalancingMethod.SolarOnly,
            AllowBatteryPower = AllowBatteryPower.Yes
        });
        var mqttSubject = new Subject<string>();

        A.CallTo(() => _mqttEntityManager.PrepareCommandSubscriptionAsync(mqttBalancingMethod))
            .Returns(Task.FromResult<IObservable<string>>(mqttSubject));

        var consumer = CarChargerEnergyConsumer2Builder.Tesla()
            .Build();
        _testCtx.TriggerStateChange(consumer.CarCharger!.Cars.First().MaxBatteryPercentageSensor!, "80");

        var builder = new EnergyManager2Builder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        _ = await builder.Build();

        //Act
        mqttSubject.OnNext(BalancingMethod.SolarPreferred.ToString());

        //Assert
        A.CallTo(() => _mqttEntityManager.SetStateAsync(mqttBalancingMethod, BalancingMethod.SolarPreferred.ToString())).MustHaveHappened();
    }
}
