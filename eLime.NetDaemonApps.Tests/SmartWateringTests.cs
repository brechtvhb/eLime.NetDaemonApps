using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.Entities.Weather;
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
public class SmartWateringTests
{

    private AppTestContext _testCtx;
    private ILogger _logger;
    private IMqttEntityManager _mqttEntityManager;
    private IFileStorage _fileStorage;

    private BinarySwitch _pumpSocket;
    private NumericSensor _availableRainWaterSensor;

    private Weather _weather;
    private WeatherAttributes _averageForeast;
    private WeatherAttributes _rainyForecast;

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
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("binary_sensor.pond_overflow"), "off");

        _weather = new Weather(_testCtx.HaContext, "weather.home");
        _averageForeast = new WeatherAttributes()
        {
            Forecast = new[]
            {
                new Forecast {Condition = "Sunny", Temperature = 25, Precipitation = 0, WindSpeed = 0},
                new Forecast {Condition = "Rainy", Temperature = 23, Precipitation = 0, WindSpeed = 0},
                new Forecast {Condition = "Cloudy", Temperature = 24, Precipitation = 0, WindSpeed = 0}
            }
        };

        _testCtx.TriggerStateChangeWithAttributes(_weather, "Cloudy", _averageForeast);

        _rainyForecast = new WeatherAttributes()
        {
            Forecast = new[]
            {
                new Forecast {Condition = "Rainy", Precipitation = 3},
                new Forecast {Condition = "Rainy", Precipitation = 3},
                new Forecast {Condition = "Rainy", Precipitation = 1}
            }
        };
    }

    [TestMethod]
    public void Init_HappyFlow()
    {
        // Arrange
        var zone = new ContainerIrrigationZoneBuilder(_testCtx)
            .Build();

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
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

        Assert.AreEqual("pond", irrigation.Zones.First().Name);
    }

    //General
    [TestMethod]
    public void Valve_Turning_on_Triggers_State_Ongoing()
    {
        // Arrange
        var zone = new ContainerIrrigationZoneBuilder(_testCtx)
            .Build();

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
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
        Assert.AreEqual(NeedsWatering.Ongoing, irrigation.Zones.First().State);
        Assert.AreEqual(_testCtx.Scheduler.Now, irrigation.Zones.First().WateringStartedAt);
        Assert.AreEqual(true, irrigation.Zones.First().CurrentlyWatering);
    }


    [TestMethod]
    public void Under_FlowRate_Triggers_Second_Zone()
    {
        // Arrange
        var zone1 = new ContainerIrrigationZoneBuilder(_testCtx)
            .WithFlowRate(500)
            .Build();

        var zone2 = new ClassicIrrigationZoneBuilder(_testCtx)
            .WithFlowRate(300)
            .Build();

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .With(_pumpSocket, 1000)
            .With(_availableRainWaterSensor, 1000)
            .AddZone(zone1)
            .AddZone(zone2)
            .Build();

        zone1.SetMode(ZoneMode.Automatic);
        zone2.SetMode(ZoneMode.Automatic);
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.pond_volume"), "5500");
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("switch.pond_valve"), "on");

        //Act
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.front_yard_soil_moisture"), "34");

        //Assert
        Assert.AreEqual(NeedsWatering.Ongoing, irrigation.Zones.Single(x => x.Name == "pond").State);
        Assert.AreEqual(NeedsWatering.Yes, irrigation.Zones.Single(x => x.Name == "front yard").State);

        _testCtx.VerifySwitchTurnOn(new BinarySwitch(_testCtx.HaContext, "switch.front_yard_valve"), Moq.Times.Once);
    }

    [TestMethod]
    public void Over_FlowRate_Does_Not_Trigger_Second_Zone()
    {
        // Arrange
        var zone1 = new ContainerIrrigationZoneBuilder(_testCtx)
            .WithFlowRate(500)
            .Build();

        var zone2 = new ClassicIrrigationZoneBuilder(_testCtx)
            .WithFlowRate(600)
            .Build();

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .With(_pumpSocket, 1000)
            .With(_availableRainWaterSensor, 2000)
            .AddZone(zone1)
            .AddZone(zone2)
            .Build();

        zone1.SetMode(ZoneMode.Automatic);
        zone2.SetMode(ZoneMode.Automatic);
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.pond_volume"), "5500");
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("switch.pond_valve"), "on");


        //Act
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.front_yard_soil_moisture"), "35");


        //Assert
        Assert.AreEqual(NeedsWatering.Ongoing, irrigation.Zones.Single(x => x.Name == "pond").State);
        Assert.AreEqual(NeedsWatering.Yes, irrigation.Zones.Single(x => x.Name == "front yard").State);

        _testCtx.VerifySwitchTurnOn(new BinarySwitch(_testCtx.HaContext, "switch.front_yard_valve"), Moq.Times.Never);
    }

    [TestMethod]
    public void Critical_Has_Precedence_Over_Normal()
    {
        // Arrange
        var zone1 = new ContainerIrrigationZoneBuilder(_testCtx)
            .WithFlowRate(500)
            .Build();

        var zone2 = new ClassicIrrigationZoneBuilder(_testCtx)
            .WithFlowRate(600)
            .Build();

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .With(_pumpSocket, 1000)
            .With(_availableRainWaterSensor, 1000)
            .AddZone(zone1)
            .AddZone(zone2)
            .Build();

        zone1.SetMode(ZoneMode.Automatic);
        zone2.SetMode(ZoneMode.Automatic);
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.pond_volume"), "5500");


        //Act
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.front_yard_soil_moisture"), "28");


        //Assert
        _testCtx.VerifySwitchTurnOff(new BinarySwitch(_testCtx.HaContext, "switch.pond_valve"), Moq.Times.Never);
        _testCtx.VerifySwitchTurnOn(new BinarySwitch(_testCtx.HaContext, "switch.front_yard_valve"), Moq.Times.Once);
    }

    [TestMethod]
    public void Energy_Available_Triggers_Start()
    {
        // Arrange
        var zone1 = new ContainerIrrigationZoneBuilder(_testCtx)
            .WithFlowRate(500)
            .Build();

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .With(_pumpSocket, 1000)
            .With(_availableRainWaterSensor, 1000)
            .AddZone(zone1)
            .Build();

        irrigation.EnergyAvailable = true;
        zone1.SetMode(ZoneMode.EnergyManaged);

        //Act
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.pond_volume"), "5500");



        //Assert
        _testCtx.VerifySwitchTurnOn(new BinarySwitch(_testCtx.HaContext, "switch.pond_valve"), Moq.Times.Once);
    }

    [TestMethod]
    public void Energy_Available_After_Sensor_Update_Triggers_Start()
    {
        // Arrange
        var zone1 = new ContainerIrrigationZoneBuilder(_testCtx)
            .WithFlowRate(500)
            .Build();

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .With(_pumpSocket, 1000)
            .With(_availableRainWaterSensor, 1000)
            .AddZone(zone1)
            .Build();

        zone1.SetMode(ZoneMode.EnergyManaged);

        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.pond_volume"), "5500");


        //Act
        irrigation.EnergyAvailable = true;
        irrigation.DebounceStartWatering();

        //Assert
        _testCtx.VerifySwitchTurnOn(new BinarySwitch(_testCtx.HaContext, "switch.pond_valve"), Moq.Times.Once);
    }

    [TestMethod]
    public void No_Energy_Available_Triggers_No_start()
    {
        // Arrange
        var zone1 = new ContainerIrrigationZoneBuilder(_testCtx)
            .WithFlowRate(500)
            .Build();

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .With(_pumpSocket, 1000)
            .With(_availableRainWaterSensor, 1000)
            .AddZone(zone1)
            .Build();

        zone1.SetMode(ZoneMode.EnergyManaged);

        //Act
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.pond_volume"), "5500");


        //Assert
        _testCtx.VerifySwitchTurnOn(new BinarySwitch(_testCtx.HaContext, "switch.pond_valve"), Moq.Times.Never);
    }

    [TestMethod]
    public void No_Energy_When_Watering_Triggers_Stop()
    {
        // Arrange
        var zone1 = new ContainerIrrigationZoneBuilder(_testCtx)
            .WithFlowRate(500)
            .Build();

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .With(_pumpSocket, 1000)
            .With(_availableRainWaterSensor, 1000)
            .AddZone(zone1)
            .Build();

        irrigation.EnergyAvailable = true;
        zone1.SetMode(ZoneMode.EnergyManaged);
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("switch.pond_valve"), "on");

        //Act

        irrigation.EnergyAvailable = false;
        irrigation.DebounceStopWatering();


        //Assert
        _testCtx.VerifySwitchTurnOff(new BinarySwitch(_testCtx.HaContext, "switch.pond_valve"), Moq.Times.Once);
    }

    [TestMethod]
    public void Mode_Off_Does_Not_Trigger_Start()
    {
        // Arrange
        var zone1 = new ContainerIrrigationZoneBuilder(_testCtx)
            .WithFlowRate(500)
            .Build();

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .With(_pumpSocket, 1000)
            .With(_availableRainWaterSensor, 1000)
            .AddZone(zone1)
            .Build();

        zone1.SetMode(ZoneMode.Manual);

        //Act
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.pond_volume"), "5500");

        //Assert
        _testCtx.VerifySwitchTurnOn(new BinarySwitch(_testCtx.HaContext, "switch.pond_valve"), Moq.Times.Never);
        _testCtx.VerifyPhoneNotified("brecht", Moq.Times.Once);
    }

    [TestMethod]
    public void No_Water_When_Watering_Triggers_Stop()
    {
        // Arrange
        var zone1 = new ContainerIrrigationZoneBuilder(_testCtx)
            .WithFlowRate(500)
            .Build();

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .With(_pumpSocket, 1000)
            .With(_availableRainWaterSensor, 1000)
            .AddZone(zone1)
            .Build();

        zone1.SetMode(ZoneMode.Automatic);
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("switch.pond_valve"), "on");

        //Act
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.rainwater_volume"), "500");
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1)); //guard runs every 5 min

        //Assert
        _testCtx.VerifySwitchTurnOff(new BinarySwitch(_testCtx.HaContext, "switch.pond_valve"), Moq.Times.AtLeastOnce);
    }

    [TestMethod]
    public void Rain_Predicted_Does_Not_Trigger_Start()
    {
        // Arrange
        var zone1 = new ContainerIrrigationZoneBuilder(_testCtx)
            .WithFlowRate(500)
            .Build();

        _testCtx.TriggerStateChangeWithAttributes(_weather, "Cloudy", _rainyForecast);

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .With(_pumpSocket, 1000)
            .With(_availableRainWaterSensor, 100)
            .With(_weather, 1, 2)
            .AddZone(zone1)
            .Build();

        zone1.SetMode(ZoneMode.Automatic);

        //Act
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.pond_volume"), "5500");

        //Assert
        _testCtx.VerifySwitchTurnOn(new BinarySwitch(_testCtx.HaContext, "switch.pond_valve"), Moq.Times.Never);
    }

    [TestMethod]
    public void No_Rain_Predicted_Does_Trigger_Start()
    {
        // Arrange
        var zone1 = new ContainerIrrigationZoneBuilder(_testCtx)
            .WithFlowRate(500)
            .Build();

        _testCtx.TriggerStateChangeWithAttributes(_weather, "Cloudy", _averageForeast);

        var irrigation = new SmartIrrigationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .With(_pumpSocket, 1000)
            .With(_availableRainWaterSensor, 100)
            .With(_weather, 1, 2)
            .AddZone(zone1)
            .Build();

        zone1.SetMode(ZoneMode.Automatic);

        //Act
        _testCtx.TriggerStateChange(_testCtx.HaContext.Entity("sensor.pond_volume"), "5500");

        //Assert
        _testCtx.VerifySwitchTurnOn(new BinarySwitch(_testCtx.HaContext, "switch.pond_valve"), Moq.Times.Once);
    }

}
