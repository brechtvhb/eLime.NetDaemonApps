using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.FlexiScenes.Rooms;
using eLime.NetDaemonApps.Domain.SmartIrrigation;
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

    private BinarySwitch _pumpSocket;
    private NumericSensor _availableRainWaterSensor;

    [TestInitialize]
    public void Init()
    {
        _testCtx = AppTestContext.Create(DateTime.Now);

        _logger = A.Fake<ILogger<Room>>();
        _mqttEntityManager = A.Fake<IMqttEntityManager>();

        _pumpSocket = BinarySwitch.Create(_testCtx.HaContext, "switch.rainwater_pump");
        _testCtx.TriggerStateChange(_pumpSocket, "on");

        _availableRainWaterSensor = NumericSensor.Create(_testCtx.HaContext, "sensor.rainwater_volume");
        _testCtx.TriggerStateChange(_availableRainWaterSensor, "10000");
    }

    public Task DeDebounce()
    {
        return Task.Delay(2);
    }

    [TestMethod]
    public async Task Below_Low_Moisture_Triggers_Valve_On()
    {
        // Arrange
        var zone1 = new ClassicIrrigationZoneBuilder(_testCtx)
            .Build();

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .With(_pumpSocket, 2000)
            .With(_availableRainWaterSensor, 1000)
            .AddZone(zone1)
            .Build();

        zone1.SetMode(ZoneMode.Automatic);

        //Act
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.front_yard_soil_moisture"), "32");
        await DeDebounce();

        //Assert
        Assert.AreEqual(NeedsWatering.Yes, irrigation.Zones.First().Zone.State);
        _testCtx.VerifySwitchTurnOn(new BinarySwitch(_testCtx.HaContext, "switch.front_yard_valve"), Moq.Times.Once);
    }

    [TestMethod]
    public async Task Below_Critical_Moisture_Triggers_State_Critical()
    {
        // Arrange
        var zone1 = new ClassicIrrigationZoneBuilder(_testCtx)
            .Build();

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .With(_pumpSocket, 2000)
            .With(_availableRainWaterSensor, 1000)
            .AddZone(zone1)
            .Build();

        zone1.SetMode(ZoneMode.Automatic);

        //Act
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.front_yard_soil_moisture"), "28");
        await DeDebounce();

        //Assert
        Assert.AreEqual(NeedsWatering.Critical, irrigation.Zones.First().Zone.State);
        _testCtx.VerifySwitchTurnOn(new BinarySwitch(_testCtx.HaContext, "switch.front_yard_valve"), Moq.Times.Once);
    }

    [TestMethod]
    public async Task Above_Target_Moisture_Triggers_Valve_Off()
    {
        // Arrange
        var zone1 = new ClassicIrrigationZoneBuilder(_testCtx)
            .Build();

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .With(_pumpSocket, 2000)
            .With(_availableRainWaterSensor, 2000)
            .AddZone(zone1)
            .Build();

        zone1.SetMode(ZoneMode.Automatic);
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.front_yard_soil_moisture"), "32");
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("switch.front_yard_valve"), "on");
        await DeDebounce();

        //Act
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.front_yard_soil_moisture"), "45");
        await DeDebounce();

        //Assert
        Assert.AreEqual(NeedsWatering.No, irrigation.Zones.First().Zone.State);
        _testCtx.VerifySwitchTurnOff(new BinarySwitch(_testCtx.HaContext, "switch.front_yard_valve"), Moq.Times.Once);
    }

    [TestMethod]
    public async Task Above_MaxDuration_Triggers_Valve_Off()
    {
        // Arrange
        var zone1 = new ClassicIrrigationZoneBuilder(_testCtx)
            .WithMaxDuration(TimeSpan.FromHours(1), TimeSpan.FromHours(23))
            .Build();

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .With(_pumpSocket, 2000)
            .With(_availableRainWaterSensor, 2000)
            .AddZone(zone1)
            .Build();

        zone1.SetMode(ZoneMode.Automatic);
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.front_yard_soil_moisture"), "32");
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("switch.front_yard_valve"), "on");
        await DeDebounce();

        //Act
        _testCtx.AdvanceTimeBy(TimeSpan.FromHours(1) + TimeSpan.FromSeconds(1));
        await DeDebounce();

        //Assert
        _testCtx.VerifySwitchTurnOff(new BinarySwitch(_testCtx.HaContext, "switch.front_yard_valve"), Moq.Times.Once);
    }

    [TestMethod]
    public async Task Respects_Timeout()
    {
        // Arrange
        var zone1 = new ClassicIrrigationZoneBuilder(_testCtx)
            .WithMaxDuration(TimeSpan.FromHours(1), TimeSpan.FromHours(23))
            .Build();

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .With(_pumpSocket, 2000)
            .With(_availableRainWaterSensor, 2000)
            .AddZone(zone1)
            .Build();

        zone1.SetMode(ZoneMode.Automatic);
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.front_yard_soil_moisture"), "32");
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("switch.front_yard_valve"), "on");
        _testCtx.AdvanceTimeBy(TimeSpan.FromHours(1) + TimeSpan.FromSeconds(1));
        await DeDebounce();

        //Act
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.front_yard_soil_moisture"), "33");
        await DeDebounce();

        //Assert
        _testCtx.VerifySwitchTurnOn(new BinarySwitch(_testCtx.HaContext, "switch.front_yard_valve"), Moq.Times.Once);
    }

    [TestMethod]
    public async Task Can_Turn_On_Again_After_Timeout()
    {
        // Arrange
        var zone1 = new ClassicIrrigationZoneBuilder(_testCtx)
            .WithMaxDuration(TimeSpan.FromHours(1), TimeSpan.FromHours(23))
            .Build();

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .With(_pumpSocket, 2000)
            .With(_availableRainWaterSensor, 2000)
            .AddZone(zone1)
            .Build();

        zone1.SetMode(ZoneMode.Automatic);
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.front_yard_soil_moisture"), "32");
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("switch.front_yard_valve"), "on");
        _testCtx.AdvanceTimeBy(TimeSpan.FromHours(1) + TimeSpan.FromSeconds(1));
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.front_yard_soil_moisture"), "33");
        await DeDebounce();

        //Act
        _testCtx.AdvanceTimeBy(TimeSpan.FromHours(23) + TimeSpan.FromSeconds(1));
        await DeDebounce();

        //Assert
        _testCtx.VerifySwitchTurnOn(new BinarySwitch(_testCtx.HaContext, "switch.front_yard_valve"), Moq.Times.AtLeast(2)); //Guard could potentially be first
    }

    [TestMethod]
    public async Task Within_TimeWindow_Triggers_Valve_On()
    {
        // Arrange
        var zone1 = new ClassicIrrigationZoneBuilder(_testCtx)
            .WithTimeWindow(new TimeOnly(10, 00), new TimeOnly(12, 00))
            .Build();

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .With(_pumpSocket, 2000)
            .With(_availableRainWaterSensor, 1000)
            .AddZone(zone1)
            .Build();

        zone1.SetMode(ZoneMode.Automatic);

        var elevenOClock = DateTime.Now.Date.AddDays(1).AddHours(11);
        _testCtx.SetCurrentTime(elevenOClock);

        //Act
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.front_yard_soil_moisture"), "32");
        await DeDebounce();

        //Assert
        Assert.AreEqual(NeedsWatering.Yes, irrigation.Zones.First().Zone.State);
        _testCtx.VerifySwitchTurnOn(new BinarySwitch(_testCtx.HaContext, "switch.front_yard_valve"), Moq.Times.Once);
    }

    [TestMethod]
    public async Task Not_Within_TimeWindow_Does_Not_Trigger_Valve_On()
    {
        // Arrange
        var zone1 = new ClassicIrrigationZoneBuilder(_testCtx)
            .WithTimeWindow(new TimeOnly(10, 00), new TimeOnly(12, 00))
            .Build();

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .With(_pumpSocket, 2000)
            .With(_availableRainWaterSensor, 1000)
            .AddZone(zone1)
            .Build();

        zone1.SetMode(ZoneMode.Automatic);

        var fourOClock = DateTime.Now.Date.AddDays(1).AddHours(4);
        _testCtx.SetCurrentTime(fourOClock);

        //Act
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.front_yard_soil_moisture"), "32");
        await DeDebounce();

        //Assert
        Assert.AreEqual(NeedsWatering.Yes, irrigation.Zones.First().Zone.State);
        _testCtx.VerifySwitchTurnOn(new BinarySwitch(_testCtx.HaContext, "switch.front_yard_valve"), Moq.Times.Never);
    }

    [TestMethod]
    public async Task Past_TimeWindow_Triggers_Valve_Off()
    {
        // Arrange
        var zone1 = new ClassicIrrigationZoneBuilder(_testCtx)
            .WithTimeWindow(new TimeOnly(10, 00), new TimeOnly(12, 00))
            .Build();

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .With(_pumpSocket, 2000)
            .With(_availableRainWaterSensor, 1000)
            .AddZone(zone1)
            .Build();

        zone1.SetMode(ZoneMode.Automatic);

        var elevenOClock = DateTime.Now.Date.AddDays(1).AddHours(11);
        _testCtx.SetCurrentTime(elevenOClock);
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.front_yard_soil_moisture"), "32");
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("switch.front_yard_valve"), "on");
        await DeDebounce();

        //Act
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(61));
        await DeDebounce();

        //Assert
        Assert.AreEqual(NeedsWatering.Ongoing, irrigation.Zones.First().Zone.State);
        _testCtx.VerifySwitchTurnOff(new BinarySwitch(_testCtx.HaContext, "switch.front_yard_valve"), Moq.Times.Once);
    }

    [TestMethod]
    public async Task Within_TimeWindow_Over_2_Days_Triggers_Valve_On()
    {
        // Arrange
        var zone1 = new ClassicIrrigationZoneBuilder(_testCtx)
            .WithTimeWindow(new TimeOnly(22, 00), new TimeOnly(01, 00))
            .Build();

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .With(_pumpSocket, 2000)
            .With(_availableRainWaterSensor, 1000)
            .AddZone(zone1)
            .Build();

        zone1.SetMode(ZoneMode.Automatic);

        var elevenOClock = DateTime.Now.Date.AddDays(1).AddHours(23);
        _testCtx.SetCurrentTime(elevenOClock);

        //Act
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.front_yard_soil_moisture"), "32");
        await DeDebounce();

        //Assert
        Assert.AreEqual(NeedsWatering.Yes, irrigation.Zones.First().Zone.State);
        _testCtx.VerifySwitchTurnOn(new BinarySwitch(_testCtx.HaContext, "switch.front_yard_valve"), Moq.Times.Once);
    }

    [TestMethod]
    public async Task Past_TimeWindow_Over_2_Days_Triggers_Valve_Off()
    {
        // Arrange
        var zone1 = new ClassicIrrigationZoneBuilder(_testCtx)
            .WithTimeWindow(new TimeOnly(22, 00), new TimeOnly(01, 00))
            .Build();

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .With(_pumpSocket, 2000)
            .With(_availableRainWaterSensor, 1000)
            .AddZone(zone1)
            .Build();

        zone1.SetMode(ZoneMode.Automatic);

        var elevenOClock = DateTime.Now.Date.AddDays(1).AddHours(23);
        _testCtx.SetCurrentTime(elevenOClock);
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.front_yard_soil_moisture"), "32");
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("switch.front_yard_valve"), "on");
        await DeDebounce();

        //Act
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(121));
        await DeDebounce();

        //Assert
        Assert.AreEqual(NeedsWatering.Ongoing, irrigation.Zones.First().Zone.State);
        _testCtx.VerifySwitchTurnOff(new BinarySwitch(_testCtx.HaContext, "switch.front_yard_valve"), Moq.Times.Once);
    }


    //TODO: test with mix of maxduration and timewindow
}
