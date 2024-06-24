using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.ClimateEntities;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.FlexiScenes.Rooms;
using eLime.NetDaemonApps.Domain.SmartVentilation;
using eLime.NetDaemonApps.Domain.Storage;
using eLime.NetDaemonApps.Tests.Builders;
using eLime.NetDaemonApps.Tests.Helpers;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Tests;

[TestClass]
public class SmartVentilationTests
{

    private AppTestContext _testCtx;
    private ILogger _logger;
    private IMqttEntityManager _mqttEntityManager;
    private IFileStorage _fileStorage;

    private Climate _climate;
    private NumericSensor _co2Sensor;
    private NumericSensor _humiditySensor;
    private BinarySensor _awaySensor;
    private BinarySensor _sleepSensor;
    private BinarySensor _summerModeSensor;
    private NumericSensor _outdoorTemperatureSensor;
    private NumericSensor _postEwtTemperatureSensor;

    [TestInitialize]
    public void Init()
    {
        _testCtx = AppTestContext.Create(DateTime.Now);

        _logger = A.Fake<ILogger<Room>>();
        _mqttEntityManager = A.Fake<IMqttEntityManager>();
        _fileStorage = A.Fake<IFileStorage>();
        A.CallTo(() => _fileStorage.Get<VentilationFileStorage>("SmartVentilation", "SmartVentilation")).Returns(new VentilationFileStorage() { Enabled = true });

        _climate = new Climate(_testCtx.HaContext, "climate.comfod");
        _co2Sensor = new(_testCtx.HaContext, "sensor.co2");
        _humiditySensor = new(_testCtx.HaContext, "sensor.co2");
        _awaySensor = new(_testCtx.HaContext, "boolean_sensor.away");
        _sleepSensor = new(_testCtx.HaContext, "boolean_sensor.kids_sleeping");
        _summerModeSensor = new(_testCtx.HaContext, "boolean_sensor.summer");
        _outdoorTemperatureSensor = new(_testCtx.HaContext, "sensor.outdoor_temperature");
        _outdoorTemperatureSensor = new(_testCtx.HaContext, "sensor.ewt_temperature");
    }

    [TestMethod]
    public void HappyFlow()
    {
        // Arrange
        _testCtx.TriggerStateChange(_climate, "fan_only");

        //Act
        var ventilation = new VentilationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage)
            .With(_climate)
            .Build();

        //Assert
        _testCtx.VerifyFanModeSet(_climate, VentilationState.Low, Moq.Times.Once);
    }

    [TestMethod]
    public void StatePingPongGuard_ManualChangePreventsPingPong()
    {
        // Arrange
        _testCtx.TriggerStateChange(_climate, "fan_only");
        var ventilation = new VentilationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage)
            .With(_climate)
            .WithStatePingPongGuard(TimeSpan.FromMinutes(30))
            .Build();

        _climate.SetFanMode("medium");

        //Act
        _testCtx.TriggerStateChange(_co2Sensor, new EntityState { State = "1500" });
        //Assert
        _testCtx.VerifyFanModeSet(_climate, VentilationState.Low, Moq.Times.Once);
    }

    [TestMethod]
    public void StatePingPongGuard_ManualChangeRevertsAfterCooldown()
    {
        // Arrange
        _testCtx.TriggerStateChange(_climate, "fan_only");
        var ventilation = new VentilationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage)
            .With(_climate)
            .WithStatePingPongGuard(TimeSpan.FromMinutes(30))
            .Build();

        _climate.SetFanMode("medium");
        _testCtx.TriggerStateChange(_co2Sensor, new EntityState { State = "300" });

        //Act
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(31));

        //Assert
        _testCtx.VerifyFanModeSet(_climate, VentilationState.Low, Moq.Times.Exactly(2));
    }

    [TestMethod]
    public void IndoorAirQualityGuard_HighCo2IncreasesFanSpeed()
    {
        // Arrange
        _testCtx.TriggerStateChange(_climate, "fan_only");
        var ventilation = new VentilationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage)
            .With(_climate)
            .WithStatePingPongGuard(TimeSpan.FromSeconds(30))
            .WithIndoorAirQualityGuard([_co2Sensor], 800, 1000)
            .Build();

        //Act
        _testCtx.TriggerStateChange(_co2Sensor, new EntityState { State = "900" });
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(61));

        //Assert
        _testCtx.VerifyFanModeSet(_climate, VentilationState.Medium, Moq.Times.Exactly(1));
    }

    [TestMethod]
    public void IndoorAirQualityGuard_VeryHighCo2MaximizesFanSpeed()
    {
        // Arrange
        _testCtx.TriggerStateChange(_climate, "fan_only");
        var ventilation = new VentilationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage)
            .With(_climate)
            .WithStatePingPongGuard(TimeSpan.FromSeconds(30))
            .WithIndoorAirQualityGuard([_co2Sensor], 800, 1000)
            .Build();

        //Act
        _testCtx.TriggerStateChange(_co2Sensor, new EntityState { State = "1100" });
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(61));

        //Assert
        _testCtx.VerifyFanModeSet(_climate, VentilationState.High, Moq.Times.Exactly(1));
    }

    [TestMethod]
    public void IndoorAirQualityGuard_LowCo2ReturnsToBaseSpeed()
    {
        // Arrange
        _testCtx.TriggerStateChange(_climate, "fan_only");
        _testCtx.TriggerStateChange(_co2Sensor, new EntityState { State = "1100" });
        var ventilation = new VentilationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage)
            .With(_climate)
            .WithStatePingPongGuard(TimeSpan.FromSeconds(30))
            .WithIndoorAirQualityGuard([_co2Sensor], 800, 1000)
            .Build();

        //Act
        _testCtx.TriggerStateChange(_co2Sensor, new EntityState { State = "400" });
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(61));

        //Assert
        _testCtx.VerifyFanModeSet(_climate, VentilationState.Low, Moq.Times.Exactly(1));
    }


    [TestMethod]
    public void BathroomAirQualityGuard_HighHumidityIncreasesFanSpeed()
    {
        // Arrange
        _testCtx.TriggerStateChange(_climate, "fan_only");
        var ventilation = new VentilationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage)
            .With(_climate)
            .WithStatePingPongGuard(TimeSpan.FromSeconds(30))
            .WithBathroomAirQualityGuard([_humiditySensor], 70, 80)
            .Build();

        //Act
        _testCtx.TriggerStateChange(_humiditySensor, new EntityState { State = "75" });
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(61));

        //Assert
        _testCtx.VerifyFanModeSet(_climate, VentilationState.Medium, Moq.Times.Exactly(1));
    }

    [TestMethod]
    public void BathroomAirQualityGuard_VeryHighHumidityMaximizesFanSpeed()
    {
        // Arrange
        _testCtx.TriggerStateChange(_climate, "fan_only");
        var ventilation = new VentilationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage)
            .With(_climate)
            .WithStatePingPongGuard(TimeSpan.FromSeconds(30))
            .WithBathroomAirQualityGuard([_humiditySensor], 70, 80)
            .Build();

        //Act
        _testCtx.TriggerStateChange(_humiditySensor, new EntityState { State = "85" });
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(61));

        //Assert
        _testCtx.VerifyFanModeSet(_climate, VentilationState.High, Moq.Times.Exactly(1));
    }

    [TestMethod]
    public void BathroomAirQualityGuard_LowHumidityReturnsToBaseSpeed()
    {
        // Arrange
        _testCtx.TriggerStateChange(_climate, "fan_only");
        _testCtx.TriggerStateChange(_humiditySensor, new EntityState { State = "85" });
        var ventilation = new VentilationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage)
            .With(_climate)
            .WithStatePingPongGuard(TimeSpan.FromSeconds(30))
            .WithBathroomAirQualityGuard([_humiditySensor], 70, 80)
            .Build();

        //Act
        _testCtx.TriggerStateChange(_humiditySensor, new EntityState { State = "60" });
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(61));

        //Assert
        _testCtx.VerifyFanModeSet(_climate, VentilationState.Low, Moq.Times.Exactly(1));
    }

    [TestMethod]
    public void IndoorTemperatureGuard_HotIncreasesSpeed()
    {
        // Arrange
        var ventilation = new VentilationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage)
            .With(_climate)
            .WithStatePingPongGuard(TimeSpan.FromSeconds(30))
            .WithIndoorTemperatureGuard(_summerModeSensor, _outdoorTemperatureSensor, _postEwtTemperatureSensor)
            .Build();

        _testCtx.TriggerStateChangeWithAttributes(_climate, "fan_only", new { Temperature = 22, CurrentTemperature = 23 });

        //Act
        _testCtx.TriggerStateChange(_outdoorTemperatureSensor, new EntityState { State = "18" });
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(61));

        //Assert
        _testCtx.VerifyFanModeSet(_climate, VentilationState.Low, Moq.Times.Exactly(2));
    }

    [TestMethod]
    public void IndoorTemperatureGuard_VeryHotIncreasesSpeed()
    {
        // Arrange
        _testCtx.TriggerStateChange(_summerModeSensor, "on");

        var ventilation = new VentilationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage)
            .With(_climate)
            .WithStatePingPongGuard(TimeSpan.FromSeconds(30))
            .WithIndoorTemperatureGuard(_summerModeSensor, _outdoorTemperatureSensor, _postEwtTemperatureSensor)
            .Build();

        _testCtx.TriggerStateChangeWithAttributes(_climate, "fan_only", new { temperature = 22, current_temperature = 24 });

        //Act
        _testCtx.TriggerStateChange(_outdoorTemperatureSensor, new EntityState { State = "18" });
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(61));

        //Assert
        _testCtx.VerifyFanModeSet(_climate, VentilationState.Medium, Moq.Times.Exactly(1));
    }

    [TestMethod]
    public void MoldGuard_LongAwayTimeActivatesFan()
    {
        // Arrange
        _testCtx.TriggerStateChange(_awaySensor, "on");

        var ventilation = new VentilationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage)
            .With(_climate)
            .WithStatePingPongGuard(TimeSpan.FromMinutes(1))
            .WithMoldGuard(TimeSpan.FromHours(8), TimeSpan.FromHours(1))
            .WithElectricityBillGuard(_awaySensor, _sleepSensor)
            .Build();

        _climate.SetFanMode("away");
        _testCtx.TriggerStateChangeWithAttributes(_climate, "fan_only", new { fan_mode = "off" });

        //Act
        _testCtx.AdvanceTimeBy(TimeSpan.FromHours(8) + TimeSpan.FromMinutes(1));

        //Assert
        _testCtx.VerifyFanModeSet(_climate, VentilationState.Low, Moq.Times.Once);
    }

    [TestMethod]
    public void MoldGuard_FanGoesOffAfterRechargeTime()
    {
        // Arrange
        _testCtx.TriggerStateChange(_awaySensor, "on");

        var ventilation = new VentilationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage)
            .With(_climate)
            .WithStatePingPongGuard(TimeSpan.FromMinutes(1))
            .WithMoldGuard(TimeSpan.FromHours(8), TimeSpan.FromHours(1))
            .WithElectricityBillGuard(_awaySensor, _sleepSensor)
            .Build();

        _testCtx.TriggerStateChangeWithAttributes(_climate, "fan_only", new { fan_mode = "off" });

        _testCtx.AdvanceTimeBy(TimeSpan.FromHours(8) + TimeSpan.FromMinutes(1));
        _testCtx.TriggerStateChangeWithAttributes(_climate, "fan_only", new { fan_mode = "low" });

        //Act
        _testCtx.AdvanceTimeBy(TimeSpan.FromHours(1) + TimeSpan.FromMinutes(1));

        //Assert
        _testCtx.VerifyFanModeSet(_climate, VentilationState.Off, Moq.Times.Exactly(2));
    }


    [TestMethod]
    public void DryAirGuard_DryAirDecreasesFanSpeed()
    {
        // Arrange
        _testCtx.TriggerStateChange(_climate, "fan_only");
        _testCtx.TriggerStateChange(_outdoorTemperatureSensor, new EntityState { State = "5" });

        var ventilation = new VentilationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage)
            .With(_climate)
            .WithStatePingPongGuard(TimeSpan.FromSeconds(30))
            .WithDryAirGuard([_humiditySensor], 35, _outdoorTemperatureSensor, 10)
            .Build();

        //Act
        _testCtx.TriggerStateChange(_humiditySensor, new EntityState { State = "25" });
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(61));

        //Assert
        _testCtx.VerifyFanModeSet(_climate, VentilationState.Off, Moq.Times.Exactly(1));
    }


    [TestMethod]
    public void DryAirGuard_NormalAirReturnsToBaseSpeed()
    {
        // Arrange
        _testCtx.TriggerStateChange(_climate, "fan_only");
        _testCtx.TriggerStateChange(_humiditySensor, new EntityState { State = "25" });
        _testCtx.TriggerStateChange(_outdoorTemperatureSensor, new EntityState { State = "25" });

        var ventilation = new VentilationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage)
            .With(_climate)
            .WithStatePingPongGuard(TimeSpan.FromSeconds(30))
            .WithDryAirGuard([_humiditySensor], 35, _outdoorTemperatureSensor, 10)
            .Build();

        //Act
        _testCtx.TriggerStateChange(_humiditySensor, new EntityState { State = "45" });
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(61));

        //Assert
        _testCtx.VerifyFanModeSet(_climate, VentilationState.Low, Moq.Times.Exactly(2));
    }


    [TestMethod]
    public void DryAirGuard_DryAirDoesNotingWithHighOutdoorTemperatures()
    {
        // Arrange
        _testCtx.TriggerStateChange(_climate, "fan_only");
        _testCtx.TriggerStateChange(_outdoorTemperatureSensor, new EntityState { State = "15" });

        var ventilation = new VentilationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage)
            .With(_climate)
            .WithStatePingPongGuard(TimeSpan.FromSeconds(30))
            .WithDryAirGuard([_humiditySensor], 35, _outdoorTemperatureSensor, 10)
            .Build();

        //Act
        _testCtx.TriggerStateChange(_humiditySensor, new EntityState { State = "25" });
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(61));

        //Assert
        _testCtx.VerifyFanModeSet(_climate, VentilationState.Off, Moq.Times.Never);
    }

    [TestMethod]
    public void ElectricityBillGuard_FanGoesOff()
    {
        // Arrange
        var ventilation = new VentilationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage)
            .With(_climate)
            .WithStatePingPongGuard(TimeSpan.FromMinutes(1))
            .WithElectricityBillGuard(_awaySensor, _sleepSensor)
            .Build();

        //Act
        _testCtx.TriggerStateChange(_awaySensor, "on");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(61));

        //Assert
        _testCtx.VerifyFanModeSet(_climate, VentilationState.Off, Moq.Times.Exactly(1));
    }
}