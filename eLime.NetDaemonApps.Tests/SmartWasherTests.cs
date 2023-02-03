using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.FlexiScenes.Rooms;
using eLime.NetDaemonApps.Domain.FlexiScreens;
using eLime.NetDaemonApps.Domain.SmartWashers;
using eLime.NetDaemonApps.Tests.Builders;
using eLime.NetDaemonApps.Tests.Helpers;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;

namespace eLime.NetDaemonApps.Tests;

[TestClass]
public class SmartWasherTests
{

    private AppTestContext _testCtx;
    private ILogger _logger;
    private IMqttEntityManager _mqttEntityManager;

    private BinarySwitch _powerSocket;
    private NumericSensor _powerSensor;

    [TestInitialize]
    public void Init()
    {
        _testCtx = AppTestContext.Create(DateTime.Now);
        _testCtx.TriggerStateChange(new FlexiScreenEnabledSwitch(_testCtx.HaContext, "switch.smartwasher_smartwasher"), "on");

        _logger = A.Fake<ILogger<Room>>();
        _mqttEntityManager = A.Fake<IMqttEntityManager>();

        _powerSocket = BinarySwitch.Create(_testCtx.HaContext, "switch.socket_washer");
        _testCtx.TriggerStateChange(_powerSocket, "off");

        _powerSensor = NumericSensor.Create(_testCtx.HaContext, "sensor.socket_washer_power");
        _testCtx.TriggerStateChange(_powerSensor, "0");
    }

    [TestMethod]
    public void Transitions_To_Prewashing_HappyFlow()
    {
        // Arrange
        var washer = new SmartWasherBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .WithPowerSocket(_powerSocket)
            .WithPowerSensor(_powerSensor)
            .Build();

        //Act
        _testCtx.TriggerStateChange(_powerSensor, "5");

        //Assert
        Assert.AreEqual(WasherStates.PreWashing, washer.State);
    }

    [TestMethod]
    public void Transitions_To_Heating_HappyFlow()
    {
        // Arrange
        var washer = new SmartWasherBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .WithPowerSocket(_powerSocket)
            .WithPowerSensor(_powerSensor)
            .Build();

        _testCtx.TriggerStateChange(_powerSensor, "5");

        //Act
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(10));
        _testCtx.TriggerStateChange(_powerSensor, "2000");

        //Assert
        Assert.AreEqual(WasherStates.Heating, washer.State);
    }

    [TestMethod]
    public void Transitions_To_Washing_HappyFlow()
    {
        // Arrange
        var washer = new SmartWasherBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .WithPowerSocket(_powerSocket)
            .WithPowerSensor(_powerSensor)
            .Build();

        _testCtx.TriggerStateChange(_powerSensor, "5");
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(10));
        _testCtx.TriggerStateChange(_powerSensor, "2000");

        //Act
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(20));
        _testCtx.TriggerStateChange(_powerSensor, "15");

        //Assert
        Assert.AreEqual(WasherStates.Washing, washer.State);
    }

    [TestMethod]
    public void Transitions_To_Rinsing_HappyFlow()
    {
        // Arrange
        var washer = new SmartWasherBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .WithPowerSocket(_powerSocket)
            .WithPowerSensor(_powerSensor)
            .Build();

        _testCtx.TriggerStateChange(_powerSensor, "5");
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(10));
        _testCtx.TriggerStateChange(_powerSensor, "2000");
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(20));
        _testCtx.TriggerStateChange(_powerSensor, "15");

        //Act
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(5));
        _testCtx.TriggerStateChange(_powerSensor, "31");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(31));
        _testCtx.TriggerStateChange(_powerSensor, "32");

        //Assert
        Assert.AreEqual(WasherStates.Rinsing, washer.State);
    }

    [TestMethod]
    public void Transitions_To_Spinning_HappyFlow()
    {
        // Arrange
        var washer = new SmartWasherBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .WithPowerSocket(_powerSocket)
            .WithPowerSensor(_powerSensor)
            .Build();

        _testCtx.TriggerStateChange(_powerSensor, "5");
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(10));
        _testCtx.TriggerStateChange(_powerSensor, "2000");
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(20));
        _testCtx.TriggerStateChange(_powerSensor, "15");
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(5));
        _testCtx.TriggerStateChange(_powerSensor, "31");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(31));
        _testCtx.TriggerStateChange(_powerSensor, "32");

        //Act
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(45));
        _testCtx.TriggerStateChange(_powerSensor, "305");

        //Assert
        Assert.AreEqual(WasherStates.Spinning, washer.State);
    }

    [TestMethod]
    public void Transitions_To_Ready_HappyFlow()
    {
        // Arrange
        var washer = new SmartWasherBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .WithPowerSocket(_powerSocket)
            .WithPowerSensor(_powerSensor)
            .Build();

        _testCtx.TriggerStateChange(_powerSensor, "5");
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(10));
        _testCtx.TriggerStateChange(_powerSensor, "2000");
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(20));
        _testCtx.TriggerStateChange(_powerSensor, "15");
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(5));
        _testCtx.TriggerStateChange(_powerSensor, "31");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(31));
        _testCtx.TriggerStateChange(_powerSensor, "32");
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(45));
        _testCtx.TriggerStateChange(_powerSensor, "305");

        //Act
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(15));
        _testCtx.TriggerStateChange(_powerSensor, "2");

        //Assert
        Assert.AreEqual(WasherStates.Ready, washer.State);
    }

    [TestMethod]
    public void Transitions_To_Idle_HappyFlow()
    {
        // Arrange
        var washer = new SmartWasherBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .WithPowerSocket(_powerSocket)
            .WithPowerSensor(_powerSensor)
            .Build();

        _testCtx.TriggerStateChange(_powerSensor, "5");
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(10));
        _testCtx.TriggerStateChange(_powerSensor, "2000");
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(20));
        _testCtx.TriggerStateChange(_powerSensor, "15");
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(5));
        _testCtx.TriggerStateChange(_powerSensor, "31");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(31));
        _testCtx.TriggerStateChange(_powerSensor, "32");
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(45));
        _testCtx.TriggerStateChange(_powerSensor, "305");
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(15));
        _testCtx.TriggerStateChange(_powerSensor, "2");

        //Act
        _testCtx.TriggerStateChange(_powerSensor, "0.5");

        //Assert
        Assert.AreEqual(WasherStates.Idle, washer.State);
    }
}