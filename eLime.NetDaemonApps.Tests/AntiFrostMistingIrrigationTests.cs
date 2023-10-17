using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.FlexiScenes.Rooms;
using eLime.NetDaemonApps.Domain.SmartIrrigation;
using eLime.NetDaemonApps.Domain.Storage;
using eLime.NetDaemonApps.Tests.Builders;
using eLime.NetDaemonApps.Tests.Helpers;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.HassModel;

namespace eLime.NetDaemonApps.Tests;

[TestClass]
public class AntiFrostMistingIrrigationTests
{

    private AppTestContext _testCtx;
    private ILogger _logger;
    private IMqttEntityManager _mqttEntityManager;
    private IFileStorage _fileStorage;

    private BinarySwitch _pumpSocket;
    private NumericSensor _availableRainWaterSensor;

    [TestInitialize]
    public void Init()
    {
        _testCtx = AppTestContext.Create(DateTime.Now);

        _logger = A.Fake<ILogger<Room>>();
        _mqttEntityManager = A.Fake<IMqttEntityManager>();
        _fileStorage = A.Fake<IFileStorage>();

        _pumpSocket = BinarySwitch.Create(_testCtx.HaContext, "switch.rainwater_pump");
        _testCtx.TriggerStateChange(_pumpSocket, "on");

        _availableRainWaterSensor = NumericSensor.Create(_testCtx.HaContext, "sensor.rainwater_volume");
        _testCtx.TriggerStateChange(_availableRainWaterSensor, "10000");
    }

    [TestMethod]
    public void Below_Low_Temperature_Triggers_Valve_On()
    {
        // Arrange
        var zone1 = new AntiFrostMistingIrrigationZoneBuilder(_testCtx)
            .Build();

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .With(_pumpSocket, 2000)
            .With(_availableRainWaterSensor, 1000)
            .AddZone(zone1)
            .Build();

        zone1.SetMode(ZoneMode.Automatic);

        //Act
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.fruit_trees_temperature"), "0.5");


        //Assert
        Assert.AreEqual(NeedsWatering.Yes, irrigation.Zones.First().State);
        _testCtx.VerifySwitchTurnOn(new BinarySwitch(_testCtx.HaContext, "switch.fruit_trees_valve"), Moq.Times.Once);
    }

    [TestMethod]
    public void Below_Critical_Moisture_Triggers_State_Critical()
    {
        // Arrange
        var zone1 = new AntiFrostMistingIrrigationZoneBuilder(_testCtx)
            .Build();

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .With(_pumpSocket, 2000)
            .With(_availableRainWaterSensor, 1000)
            .AddZone(zone1)
            .Build();

        zone1.SetMode(ZoneMode.Automatic);

        //Act
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.fruit_trees_temperature"), "0");


        //Assert
        Assert.AreEqual(NeedsWatering.Critical, irrigation.Zones.First().State);
        _testCtx.VerifySwitchTurnOn(new BinarySwitch(_testCtx.HaContext, "switch.fruit_trees_valve"), Moq.Times.Once);
    }

    [TestMethod]
    public void End_Of_Misting_Duration_Triggers_Valve_Off()
    {
        // Arrange
        var zone1 = new AntiFrostMistingIrrigationZoneBuilder(_testCtx)
            .WithMistingDurations(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5))
            .Build();

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .With(_pumpSocket, 2000)
            .With(_availableRainWaterSensor, 2000)
            .AddZone(zone1)
            .Build();

        zone1.SetMode(ZoneMode.Automatic);
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.fruit_trees_temperature"), "0");
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("switch.fruit_trees_valve"), "on");


        //Act
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));


        //Assert
        _testCtx.VerifySwitchTurnOff(new BinarySwitch(_testCtx.HaContext, "switch.fruit_trees_valve"), Moq.Times.Once);
    }

    [TestMethod]
    public void Respects_Timeout()
    {
        // Arrange
        var zone1 = new AntiFrostMistingIrrigationZoneBuilder(_testCtx)
            .WithMistingDurations(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5))
            .Build();

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .With(_pumpSocket, 2000)
            .With(_availableRainWaterSensor, 2000)
            .AddZone(zone1)
            .Build();

        zone1.SetMode(ZoneMode.Automatic);
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("switch.fruit_trees_valve"), "on");


        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("switch.fruit_trees_valve"), "off");

        //Act
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.fruit_trees_temperature"), "0.1");


        //Assert
        Assert.AreEqual(false, zone1.CanStartWatering(_testCtx.Scheduler.Now, true));
        _testCtx.VerifySwitchTurnOn(new BinarySwitch(_testCtx.HaContext, "switch.front_yard_valve"), Moq.Times.Never);
    }

    [TestMethod]
    public void Can_Turn_On_Again_After_Timeout()
    {
        // Arrange
        var zone1 = new AntiFrostMistingIrrigationZoneBuilder(_testCtx)
            .WithMistingDurations(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1))
            .Build();

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .With(_pumpSocket, 2000)
            .With(_availableRainWaterSensor, 2000)
            .AddZone(zone1)
            .Build();

        zone1.SetMode(ZoneMode.Automatic);
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("switch.fruit_trees_valve"), "on");
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.fruit_trees_temperature"), "0.1");

        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("switch.fruit_trees_valve"), "off");

        //Act
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));

        //Assert
        _testCtx.VerifySwitchTurnOn(new BinarySwitch(_testCtx.HaContext, "switch.fruit_trees_valve"), Moq.Times.AtLeastOnce); //Guard could potentially be first
    }
}
