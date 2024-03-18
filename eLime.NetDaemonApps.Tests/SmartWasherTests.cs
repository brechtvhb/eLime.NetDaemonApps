using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.FlexiScenes.Rooms;
using eLime.NetDaemonApps.Domain.SmartWashers;
using eLime.NetDaemonApps.Domain.Storage;
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
    private IFileStorage _fileStorage;

    private BinarySwitch _powerSocket;
    private NumericSensor _powerSensor;

    [TestInitialize]
    public void Init()
    {
        _testCtx = AppTestContext.Create(DateTime.Now);

        _logger = A.Fake<ILogger<Room>>();
        _mqttEntityManager = A.Fake<IMqttEntityManager>();

        _fileStorage = A.Fake<IFileStorage>();
        A.CallTo(() => _fileStorage.Get<SmartWasherFileStorage>("Smartwasher", "smartwasher")).Returns(new SmartWasherFileStorage { Enabled = true });

        _powerSocket = BinarySwitch.Create(_testCtx.HaContext, "switch.socket_washer");
        _testCtx.TriggerStateChange(_powerSocket, "off");

        _powerSensor = NumericSensor.Create(_testCtx.HaContext, "sensor.socket_washer_power");
        _testCtx.TriggerStateChange(_powerSensor, "0");
    }

    [TestMethod]
    public void Transitions_To_Prewashing_HappyFlow()
    {
        // Arrange
        var washer = new SmartWasherBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler, _fileStorage)
            .WithPowerSocket(_powerSocket)
            .WithPowerSensor(_powerSensor)
            .Build();

        //Act
        _testCtx.TriggerStateChange(_powerSensor, "10");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(20));
        _testCtx.TriggerStateChange(_powerSensor, "50");

        //Assert
        Assert.AreEqual(WasherStates.PreWashing, washer.State);
    }


    [TestMethod]
    public void Resets_After_10_Minutes()
    {
        // Arrange
        var washer = new SmartWasherBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler, _fileStorage)
            .WithPowerSocket(_powerSocket)
            .WithPowerSensor(_powerSensor)
            .Build();

        _testCtx.TriggerStateChange(_powerSensor, "10");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(20));
        _testCtx.TriggerStateChange(_powerSensor, "50");

        //Act
        _testCtx.TriggerStateChange(_powerSensor, "0");
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(11));

        //Assert
        Assert.AreEqual(WasherStates.Ready, washer.State);
    }

    [TestMethod]
    public void Transitions_To_Heating_HappyFlow()
    {
        // Arrange
        var washer = new SmartWasherBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler, _fileStorage)
            .WithPowerSocket(_powerSocket)
            .WithPowerSensor(_powerSensor)
            .Build();

        _testCtx.TriggerStateChange(_powerSensor, "10");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(20));
        _testCtx.TriggerStateChange(_powerSensor, "50");

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
        var washer = new SmartWasherBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler, _fileStorage)
            .WithPowerSocket(_powerSocket)
            .WithPowerSensor(_powerSensor)
            .Build();

        _testCtx.TriggerStateChange(_powerSensor, "10");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(20));
        _testCtx.TriggerStateChange(_powerSensor, "50");
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
        var washer = new SmartWasherBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler, _fileStorage)
            .WithPowerSocket(_powerSocket)
            .WithPowerSensor(_powerSensor)
            .Build();

        _testCtx.TriggerStateChange(_powerSensor, "10");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(20));
        _testCtx.TriggerStateChange(_powerSensor, "50");
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
        var washer = new SmartWasherBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler, _fileStorage)
            .WithPowerSocket(_powerSocket)
            .WithPowerSensor(_powerSensor)
            .Build();

        _testCtx.TriggerStateChange(_powerSensor, "10");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(20));
        _testCtx.TriggerStateChange(_powerSensor, "50");
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
        var washer = new SmartWasherBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler, _fileStorage)
            .WithPowerSocket(_powerSocket)
            .WithPowerSensor(_powerSensor)
            .Build();

        _testCtx.TriggerStateChange(_powerSensor, "10");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(20));
        _testCtx.TriggerStateChange(_powerSensor, "50");
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
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(2));
        _testCtx.TriggerStateChange(_powerSensor, "3");

        //Assert
        Assert.AreEqual(WasherStates.Ready, washer.State);
    }

    [TestMethod]
    public void Transitions_To_Idle_HappyFlow()
    {
        // Arrange
        var washer = new SmartWasherBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler, _fileStorage)
            .WithPowerSocket(_powerSocket)
            .WithPowerSensor(_powerSensor)
            .Build();

        _testCtx.TriggerStateChange(_powerSensor, "10");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(20));
        _testCtx.TriggerStateChange(_powerSensor, "50");
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
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(2));
        _testCtx.TriggerStateChange(_powerSensor, "3");

        //Act
        _testCtx.TriggerStateChange(_powerSensor, "0.5");

        //Assert
        Assert.AreEqual(WasherStates.Idle, washer.State);
    }

    [TestMethod]
    public void Transitions_To_PreWashing_From_Ready()
    {
        // Arrange
        var washer = new SmartWasherBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler, _fileStorage)
            .WithPowerSocket(_powerSocket)
            .WithPowerSensor(_powerSensor)
            .Build();

        _testCtx.TriggerStateChange(_powerSensor, "10");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(20));
        _testCtx.TriggerStateChange(_powerSensor, "50");
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
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(2));
        _testCtx.TriggerStateChange(_powerSensor, "3");

        //Act
        _testCtx.TriggerStateChange(_powerSensor, "10");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(20));
        _testCtx.TriggerStateChange(_powerSensor, "50");

        //Assert
        Assert.AreEqual(WasherStates.PreWashing, washer.State);
    }

    [TestMethod]
    public void Transitions_To_DelayedStart_If_Switch_On()
    {
        // Arrange
        _fileStorage = A.Fake<IFileStorage>();
        A.CallTo(() => _fileStorage.Get<SmartWasherFileStorage>("Smartwasher", "smartwasher")).Returns(new SmartWasherFileStorage { Enabled = true, CanDelayStart = true });

        var washer = new SmartWasherBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler, _fileStorage)
            .WithPowerSocket(_powerSocket)
            .WithPowerSensor(_powerSensor)
            .Build();

        //Act
        _testCtx.TriggerStateChange(_powerSensor, "10");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(20));
        _testCtx.TriggerStateChange(_powerSensor, "50");

        //Assert
        Assert.AreEqual(WasherStates.DelayedStart, washer.State);
        _testCtx.VerifySwitchTurnOff(_powerSocket, Moq.Times.Once);
    }

    [TestMethod]
    public void Does_Not_Transition_To_DelayedStart_Instantly()
    {
        // Arrange
        _fileStorage = A.Fake<IFileStorage>();
        A.CallTo(() => _fileStorage.Get<SmartWasherFileStorage>("Smartwasher", "smartwasher")).Returns(new SmartWasherFileStorage { Enabled = true, CanDelayStart = true });

        var washer = new SmartWasherBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler, _fileStorage)
            .WithPowerSocket(_powerSocket)
            .WithPowerSensor(_powerSensor)
            .Build();

        //Act
        _testCtx.TriggerStateChange(_powerSensor, "10");

        //Assert
        Assert.AreEqual(WasherStates.Idle, washer.State);
    }

    [TestMethod]
    public void Awakens_From_Delayed_Start_By_Turning_Socket_On_On_Trigger()
    {
        // Arrange
        _fileStorage = A.Fake<IFileStorage>();
        A.CallTo(() => _fileStorage.Get<SmartWasherFileStorage>("Smartwasher", "smartwasher")).Returns(new SmartWasherFileStorage { Enabled = true, CanDelayStart = true });

        var washer = new SmartWasherBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler, _fileStorage)
            .WithPowerSocket(_powerSocket)
            .WithPowerSensor(_powerSensor)
            .Build();

        _testCtx.TriggerStateChange(_powerSensor, "10");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(20));
        _testCtx.TriggerStateChange(_powerSensor, "50");

        //Act
        washer.DelayedStartTriggerHandler().Invoke("ON");

        //Assert
        _testCtx.VerifySwitchTurnOn(_powerSocket, Moq.Times.Once);
    }

    [TestMethod]
    public void Transitions_From_DelayedStart_To_PreWashing_If_Socket_Turns_On()
    {
        // Arrange
        _fileStorage = A.Fake<IFileStorage>();
        A.CallTo(() => _fileStorage.Get<SmartWasherFileStorage>("Smartwasher", "smartwasher")).Returns(new SmartWasherFileStorage { Enabled = true, CanDelayStart = true, DelayedStartTriggered = true });

        var washer = new SmartWasherBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler, _fileStorage)
            .WithPowerSocket(_powerSocket)
            .WithPowerSensor(_powerSensor)
            .Build();

        _testCtx.TriggerStateChange(_powerSensor, "10");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(20));
        _testCtx.TriggerStateChange(_powerSensor, "50");

        //Act
        _testCtx.TriggerStateChange(_powerSocket, "on");

        //Assert
        Assert.AreEqual(WasherStates.PreWashing, washer.State);
    }

    [TestMethod]
    public void Does_Nothing_If_Socket_Turns_On_In_Wrong_State()
    {
        // Arrange
        _fileStorage = A.Fake<IFileStorage>();
        A.CallTo(() => _fileStorage.Get<SmartWasherFileStorage>("Smartwasher", "smartwasher")).Returns(new SmartWasherFileStorage { Enabled = true, CanDelayStart = true });

        var washer = new SmartWasherBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler, _fileStorage)
            .WithPowerSocket(_powerSocket)
            .WithPowerSensor(_powerSensor)
            .Build();

        //Act
        _testCtx.TriggerStateChange(_powerSocket, "on");

        //Assert
        Assert.AreEqual(WasherStates.Idle, washer.State);
    }
}