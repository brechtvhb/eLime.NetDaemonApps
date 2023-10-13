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
public class ClassicIrrigationTests
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
    public void Below_Low_Moisture_Triggers_Valve_On()
    {
        // Arrange
        var zone1 = new ClassicIrrigationZoneBuilder(_testCtx)
            .Build();

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .With(_pumpSocket, 2000)
            .With(_availableRainWaterSensor, 1000)
            .AddZone(zone1)
            .Build();

        zone1.SetMode(ZoneMode.Automatic);

        //Act
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.front_yard_soil_moisture"), "32");


        //Assert
        Assert.AreEqual(NeedsWatering.Yes, irrigation.Zones.First().Zone.State);
        _testCtx.VerifySwitchTurnOn(new BinarySwitch(_testCtx.HaContext, "switch.front_yard_valve"), Moq.Times.Once);
    }

    [TestMethod]
    public void Below_Critical_Moisture_Triggers_State_Critical()
    {
        // Arrange
        var zone1 = new ClassicIrrigationZoneBuilder(_testCtx)
            .Build();

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .With(_pumpSocket, 2000)
            .With(_availableRainWaterSensor, 1000)
            .AddZone(zone1)
            .Build();

        zone1.SetMode(ZoneMode.Automatic);

        //Act
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.front_yard_soil_moisture"), "28");


        //Assert
        Assert.AreEqual(NeedsWatering.Critical, irrigation.Zones.First().Zone.State);
        _testCtx.VerifySwitchTurnOn(new BinarySwitch(_testCtx.HaContext, "switch.front_yard_valve"), Moq.Times.Once);
    }

    [TestMethod]
    public void Above_Target_Moisture_Triggers_Valve_Off()
    {
        // Arrange
        var zone1 = new ClassicIrrigationZoneBuilder(_testCtx)
            .Build();

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .With(_pumpSocket, 2000)
            .With(_availableRainWaterSensor, 2000)
            .AddZone(zone1)
            .Build();

        zone1.SetMode(ZoneMode.Automatic);
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.front_yard_soil_moisture"), "32");
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("switch.front_yard_valve"), "on");


        //Act
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.front_yard_soil_moisture"), "45");


        //Assert
        Assert.AreEqual(NeedsWatering.No, irrigation.Zones.First().Zone.State);
        _testCtx.VerifySwitchTurnOff(new BinarySwitch(_testCtx.HaContext, "switch.front_yard_valve"), Moq.Times.Once);
    }

    [TestMethod]
    public void Above_MaxDuration_Triggers_Valve_Off()
    {
        // Arrange
        var zone1 = new ClassicIrrigationZoneBuilder(_testCtx)
            .WithMaxDuration(TimeSpan.FromHours(1), TimeSpan.FromHours(23))
            .Build();

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .With(_pumpSocket, 2000)
            .With(_availableRainWaterSensor, 2000)
            .AddZone(zone1)
            .Build();

        zone1.SetMode(ZoneMode.Automatic);
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.front_yard_soil_moisture"), "32");
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("switch.front_yard_valve"), "on");


        //Act
        _testCtx.AdvanceTimeBy(TimeSpan.FromHours(1) + TimeSpan.FromSeconds(1));


        //Assert
        _testCtx.VerifySwitchTurnOff(new BinarySwitch(_testCtx.HaContext, "switch.front_yard_valve"), Moq.Times.Once);
    }

    [TestMethod]
    public void Respects_Timeout()
    {
        // Arrange
        var zone1 = new ClassicIrrigationZoneBuilder(_testCtx)
            .WithMaxDuration(TimeSpan.FromHours(1), TimeSpan.FromHours(23))
            .Build();

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .With(_pumpSocket, 2000)
            .With(_availableRainWaterSensor, 2000)
            .AddZone(zone1)
            .Build();

        zone1.SetMode(ZoneMode.Automatic);
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("switch.front_yard_valve"), "on");


        _testCtx.AdvanceTimeBy(TimeSpan.FromHours(1) + TimeSpan.FromSeconds(1));
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("switch.front_yard_valve"), "off");


        //Act
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.front_yard_soil_moisture"), "33");


        //Assert
        Assert.AreEqual(false, zone1.CanStartWatering(_testCtx.Scheduler.Now, true));
        _testCtx.VerifySwitchTurnOn(new BinarySwitch(_testCtx.HaContext, "switch.front_yard_valve"), Moq.Times.Never);
    }

    [TestMethod]
    public void Can_Turn_On_Again_After_Timeout()
    {
        // Arrange
        var zone1 = new ClassicIrrigationZoneBuilder(_testCtx)
            .WithMaxDuration(TimeSpan.FromHours(1), TimeSpan.FromHours(23))
            .Build();

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .With(_pumpSocket, 2000)
            .With(_availableRainWaterSensor, 2000)
            .AddZone(zone1)
            .Build();

        zone1.SetMode(ZoneMode.Automatic);
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("switch.front_yard_valve"), "on");


        _testCtx.AdvanceTimeBy(TimeSpan.FromHours(1) + TimeSpan.FromSeconds(1));
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.front_yard_soil_moisture"), "33");
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("switch.front_yard_valve"), "off");


        //Act
        _testCtx.AdvanceTimeBy(TimeSpan.FromHours(23) + TimeSpan.FromSeconds(1));


        //Assert
        _testCtx.VerifySwitchTurnOn(new BinarySwitch(_testCtx.HaContext, "switch.front_yard_valve"), Moq.Times.AtLeastOnce); //Guard could potentially be first
    }

    [TestMethod]
    public void Within_TimeWindow_Triggers_Valve_On()
    {
        // Arrange
        var zone1 = new ClassicIrrigationZoneBuilder(_testCtx)
            .WithTimeWindow(new TimeOnly(10, 00), new TimeOnly(12, 00))
            .Build();

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .With(_pumpSocket, 2000)
            .With(_availableRainWaterSensor, 1000)
            .AddZone(zone1)
            .Build();

        zone1.SetMode(ZoneMode.Automatic);

        var elevenOClock = DateTime.Now.Date.AddDays(1).AddHours(11);
        _testCtx.SetCurrentTime(elevenOClock);

        //Act
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.front_yard_soil_moisture"), "32");


        //Assert
        Assert.AreEqual(NeedsWatering.Yes, irrigation.Zones.First().Zone.State);
        _testCtx.VerifySwitchTurnOn(new BinarySwitch(_testCtx.HaContext, "switch.front_yard_valve"), Moq.Times.Once);
    }

    [TestMethod]
    public void Not_Within_TimeWindow_Does_Not_Trigger_Valve_On()
    {
        // Arrange
        var zone1 = new ClassicIrrigationZoneBuilder(_testCtx)
            .WithTimeWindow(new TimeOnly(10, 00), new TimeOnly(12, 00))
            .Build();

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .With(_pumpSocket, 2000)
            .With(_availableRainWaterSensor, 1000)
            .AddZone(zone1)
            .Build();

        zone1.SetMode(ZoneMode.Automatic);

        var fourOClock = DateTime.Now.Date.AddDays(1).AddHours(4);
        _testCtx.SetCurrentTime(fourOClock);

        //Act
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.front_yard_soil_moisture"), "32");


        //Assert
        Assert.AreEqual(NeedsWatering.Yes, irrigation.Zones.First().Zone.State);
        _testCtx.VerifySwitchTurnOn(new BinarySwitch(_testCtx.HaContext, "switch.front_yard_valve"), Moq.Times.Never);
    }

    [TestMethod]
    public void Past_TimeWindow_Triggers_Valve_Off()
    {
        // Arrange
        var zone1 = new ClassicIrrigationZoneBuilder(_testCtx)
            .WithTimeWindow(new TimeOnly(10, 00), new TimeOnly(12, 00))
            .Build();

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .With(_pumpSocket, 2000)
            .With(_availableRainWaterSensor, 1000)
            .AddZone(zone1)
            .Build();

        zone1.SetMode(ZoneMode.Automatic);

        var elevenOClock = DateTime.Now.Date.AddDays(1).AddHours(11);
        _testCtx.SetCurrentTime(elevenOClock);
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.front_yard_soil_moisture"), "32");
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("switch.front_yard_valve"), "on");


        //Act
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(61));


        //Assert
        Assert.AreEqual(NeedsWatering.Ongoing, irrigation.Zones.First().Zone.State);
        _testCtx.VerifySwitchTurnOff(new BinarySwitch(_testCtx.HaContext, "switch.front_yard_valve"), Moq.Times.Once);
    }

    [TestMethod]
    public void Within_TimeWindow_Over_2_Days_Triggers_Valve_On()
    {
        // Arrange
        var zone1 = new ClassicIrrigationZoneBuilder(_testCtx)
            .WithTimeWindow(new TimeOnly(22, 00), new TimeOnly(01, 00))
            .Build();

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .With(_pumpSocket, 2000)
            .With(_availableRainWaterSensor, 1000)
            .AddZone(zone1)
            .Build();

        zone1.SetMode(ZoneMode.Automatic);

        var elevenOClock = DateTime.Now.Date.AddDays(1).AddHours(23);
        _testCtx.SetCurrentTime(elevenOClock);

        //Act
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.front_yard_soil_moisture"), "32");


        //Assert
        Assert.AreEqual(NeedsWatering.Yes, irrigation.Zones.First().Zone.State);
        _testCtx.VerifySwitchTurnOn(new BinarySwitch(_testCtx.HaContext, "switch.front_yard_valve"), Moq.Times.Once);
    }

    [TestMethod]
    public void Past_TimeWindow_Over_2_Days_Triggers_Valve_Off()
    {
        // Arrange
        var zone1 = new ClassicIrrigationZoneBuilder(_testCtx)
            .WithTimeWindow(new TimeOnly(22, 00), new TimeOnly(01, 00))
            .Build();

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .With(_pumpSocket, 2000)
            .With(_availableRainWaterSensor, 1000)
            .AddZone(zone1)
            .Build();

        zone1.SetMode(ZoneMode.Automatic);

        var elevenOClock = DateTime.Now.Date.AddDays(1).AddHours(23);
        _testCtx.SetCurrentTime(elevenOClock);
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.front_yard_soil_moisture"), "32");
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("switch.front_yard_valve"), "on");


        //Act
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(121));


        //Assert
        Assert.AreEqual(NeedsWatering.Ongoing, irrigation.Zones.First().Zone.State);
        _testCtx.VerifySwitchTurnOff(new BinarySwitch(_testCtx.HaContext, "switch.front_yard_valve"), Moq.Times.Once);
    }


    [TestMethod]
    public void Past_TimeWindow_And_Larger_Max_Duration_Triggers_Valve_Off_At_End_Of_Window()
    {
        // Arrange
        var zone1 = new ClassicIrrigationZoneBuilder(_testCtx)
            .WithMaxDuration(TimeSpan.FromHours(3), TimeSpan.FromHours(23))
            .WithTimeWindow(new TimeOnly(10, 00), new TimeOnly(12, 00))
            .Build();

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .With(_pumpSocket, 2000)
            .With(_availableRainWaterSensor, 1000)
            .AddZone(zone1)
            .Build();

        zone1.SetMode(ZoneMode.Automatic);

        var elevenOClock = DateTime.Now.Date.AddDays(1).AddHours(11);
        _testCtx.SetCurrentTime(elevenOClock);
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.front_yard_soil_moisture"), "32");
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("switch.front_yard_valve"), "on");


        //Act
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(61));


        //Assert
        Assert.AreEqual(NeedsWatering.Ongoing, irrigation.Zones.First().Zone.State);
        _testCtx.VerifySwitchTurnOff(new BinarySwitch(_testCtx.HaContext, "switch.front_yard_valve"), Moq.Times.Once);
    }

    [TestMethod]
    public void In_TimeWindow_And_Smaller_Max_Duration_Triggers_Valve_Off_At_End_Of_Duration()
    {
        // Arrange
        var zone1 = new ClassicIrrigationZoneBuilder(_testCtx)
            .WithMaxDuration(TimeSpan.FromMinutes(30), TimeSpan.FromHours(23))
            .WithTimeWindow(new TimeOnly(10, 00), new TimeOnly(12, 00))
            .Build();

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .With(_pumpSocket, 2000)
            .With(_availableRainWaterSensor, 1000)
            .AddZone(zone1)
            .Build();

        zone1.SetMode(ZoneMode.Automatic);

        var elevenOClock = DateTime.Now.Date.AddDays(1).AddHours(11);
        _testCtx.SetCurrentTime(elevenOClock);
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.front_yard_soil_moisture"), "32");
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("switch.front_yard_valve"), "on");


        //Act
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(31));


        //Assert
        Assert.AreEqual(NeedsWatering.Ongoing, irrigation.Zones.First().Zone.State);
        _testCtx.VerifySwitchTurnOff(new BinarySwitch(_testCtx.HaContext, "switch.front_yard_valve"), Moq.Times.Once);
    }
}
