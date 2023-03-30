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
public class SmartWateringTests
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

    [TestMethod]
    public void Init_HappyFlow()
    {
        // Arrange
        var zone = new ContainerIrrigationZoneBuilder(_testCtx)
            .Build();

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .With(_pumpSocket, 2000)
            .With(_availableRainWaterSensor, 1000)
        .AddZone(zone)
        .Build();

        //Act

        //Assert
        Assert.AreEqual(_pumpSocket.EntityId, irrigation.PumpSocket.EntityId);
        Assert.AreEqual(2000, irrigation.PumpFlowRate);

        Assert.AreEqual(_availableRainWaterSensor.EntityId, irrigation.AvailableRainWaterSensor.EntityId);
        Assert.AreEqual(1000, irrigation.MinimumAvailableRainWater);

        Assert.AreEqual("pond", irrigation.Zones.First().Zone.Name);
    }

    [TestMethod]
    public void Below_Low_Volume_Triggers_Valve_On()
    {
        // Arrange
        var zone = new ContainerIrrigationZoneBuilder(_testCtx)
            .Build();

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .With(_pumpSocket, 2000)
            .With(_availableRainWaterSensor, 2000)
            .AddZone(zone)
            .Build();

        zone.SetMode(ZoneMode.Automatic);
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("binary_sensor.pond_overflow"), "off");

        //Act
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.pond_volume"), "5500");

        //Assert
        Assert.AreEqual(NeedsWatering.Yes, irrigation.Zones.First().Zone.State);
        _testCtx.VerifySwitchTurnOn(new BinarySwitch(_testCtx.HaContext, "switch.pond_valve"), Moq.Times.Once);
    }


    [TestMethod]
    public void Valve_Turning_on_Triggers_StateChange()
    {
        // Arrange
        var zone = new ContainerIrrigationZoneBuilder(_testCtx)
            .Build();

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .With(_pumpSocket, 2000)
            .With(_availableRainWaterSensor, 2000)
            .AddZone(zone)
            .Build();

        zone.SetMode(ZoneMode.Automatic);
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.pond_volume"), "5500");
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("binary_sensor.pond_overflow"), "off");

        //Act
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("switch.pond_valve"), "on");

        //Assert
        Assert.AreEqual(NeedsWatering.Ongoing, irrigation.Zones.First().Zone.State);
        Assert.AreEqual(_testCtx.Scheduler.Now, irrigation.Zones.First().Zone.WateringStartedAt);
    }

    [TestMethod]
    public void Overflow_On_Triggers_Valve_Off()
    {
        // Arrange
        var zone = new ContainerIrrigationZoneBuilder(_testCtx)
            .Build();

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _testCtx.Scheduler)
            .With(_pumpSocket, 2000)
            .With(_availableRainWaterSensor, 2000)
            .AddZone(zone)
            .Build();

        zone.SetMode(ZoneMode.Automatic);
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.pond_volume"), "5500");
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("binary_sensor.pond_overflow"), "off");
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("switch.pond_valve"), "on");

        //Act
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("binary_sensor.pond_overflow"), "on");

        //Assert
        Assert.AreEqual(NeedsWatering.No, irrigation.Zones.First().Zone.State);
        _testCtx.VerifySwitchTurnOff(new BinarySwitch(_testCtx.HaContext, "switch.pond_valve"), Moq.Times.Once);
    }
}