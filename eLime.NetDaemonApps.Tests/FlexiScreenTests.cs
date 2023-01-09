using eLime.NetDaemonApps.Domain.Entities.Covers;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.Entities.Sun;
using eLime.NetDaemonApps.Domain.FlexiScenes.Rooms;
using eLime.NetDaemonApps.Domain.FlexiScreens;
using eLime.NetDaemonApps.Tests.Builders;
using eLime.NetDaemonApps.Tests.Helpers;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Tests;

[TestClass]
public class FlexiScreenTests
{

    private AppTestContext _testCtx;
    private ILogger _logger;
    private IMqttEntityManager _mqttEntityManager;

    private Cover _cover;
    private Sun _sun;
    private NumericThresholdSensor _windSpeedSensor;
    private NumericThresholdSensor _rainRateSensor;
    private NumericThresholdSensor _shortTermRainForecastSensor;

    [TestInitialize]
    public void Init()
    {
        _testCtx = AppTestContext.Create(DateTime.Now);
        _testCtx.TriggerStateChange(new FlexiScreenEnabledSwitch(_testCtx.HaContext, "switch.flexiscreen_office"), "on");

        _logger = A.Fake<ILogger<Room>>();
        _mqttEntityManager = A.Fake<IMqttEntityManager>();


        _cover = new Cover(_testCtx.HaContext, "cover.office");
        _testCtx.TriggerStateChange(_cover, "open");

        _sun = new Sun(_testCtx.HaContext, "sun.sun");
        _windSpeedSensor = NumericThresholdSensor.Create(_testCtx.HaContext, "sensor.windspeed", 60, 40);
        _testCtx.TriggerStateChange(_windSpeedSensor, new EntityState { State = "0" });

        _rainRateSensor = NumericThresholdSensor.Create(_testCtx.HaContext, "sensor.rainrate", 2, 0);
        _testCtx.TriggerStateChange(_rainRateSensor, new EntityState { State = "0" });

        _shortTermRainForecastSensor = NumericThresholdSensor.Create(_testCtx.HaContext, "sensor.rainforecast", 0.2d, 0);
        _testCtx.TriggerStateChange(_shortTermRainForecastSensor, new EntityState { State = "0" });
    }

    [TestMethod]
    public void SunInPosition_Closes_Screen()
    {
        // Arrange
        _testCtx.TriggerStateChange(_cover, "open");
        var screen = new ScreenBuilder(_testCtx, _logger, _mqttEntityManager)
            .WithCover(_cover)
            .WithSun(_sun)
            .Build();

        //Act
        var sunAttributes = new SunAttributes
        {
            Azimuth = 240,
            Elevation = 20,
        };

        _testCtx.TriggerStateChangeWithAttributes(_sun, "below_horizon", sunAttributes);

        //Assert
        _testCtx.VerifyScreenGoesDown(_cover, Moq.Times.Once);
    }

    [TestMethod]
    public void SunBelowElevation_Puts_ScreenInCorrectPosition()
    {
        // Arrange
        var screen = new ScreenBuilder(_testCtx, _logger, _mqttEntityManager)
            .WithCover(_cover)
            .WithSun(_sun)
            .Build();

        //Act
        var sunAttributes = new SunAttributes
        {
            Azimuth = 0,
            Elevation = -20,
        };

        _testCtx.TriggerStateChangeWithAttributes(_sun, "above_horizon", sunAttributes);

        //Assert
        _testCtx.VerifyScreenGoesDown(_cover, Moq.Times.Once);
    }

    [TestMethod]
    public void SunOutOfPosition_Opens_Screen()
    {
        // Arrange
        _testCtx.TriggerStateChange(_cover, "closed");
        var screen = new ScreenBuilder(_testCtx, _logger, _mqttEntityManager)
            .WithCover(_cover)
            .WithSun(_sun)
            .Build();

        //Act
        var sunAttributes = new SunAttributes
        {
            Azimuth = 90,
            Elevation = 20,
        };

        _testCtx.TriggerStateChangeWithAttributes(_sun, "above_horizon", sunAttributes);

        //Assert
        _testCtx.VerifyScreenGoesUp(_cover, Moq.Times.Once);
    }

    [TestMethod]
    public void Wind_Closes_Screen()
    {
        // Arrange
        var screen = new ScreenBuilder(_testCtx, _logger, _mqttEntityManager)
            .WithCover(_cover)
            .WithSun(_sun)
            .WithWindSpeedSensor(_windSpeedSensor)
            .Build();

        //Act
        _testCtx.TriggerStateChange(_windSpeedSensor, new EntityState { State = "80" });

        //Assert
        Assert.AreEqual((ScreenState.Down, true), screen.StormProtector.DesiredState);
        _testCtx.VerifyScreenGoesDown(_cover, Moq.Times.Once);
    }

    [TestMethod]
    public void NoWind_DoesNot_Close_Screen()
    {
        // Arrange
        var screen = new ScreenBuilder(_testCtx, _logger, _mqttEntityManager)
            .WithCover(_cover)
            .WithSun(_sun)
            .WithWindSpeedSensor(_windSpeedSensor)
            .Build();

        //Act
        _testCtx.TriggerStateChange(_windSpeedSensor, new EntityState { State = "45" });

        //Assert
        _testCtx.VerifyScreenGoesDown(_cover, Moq.Times.Never);
    }

    [TestMethod]
    public void NoWind_AfterStorm_Does_Nothing()
    {
        // Arrange
        _testCtx.TriggerStateChange(_windSpeedSensor, new EntityState { State = "80" });
        _testCtx.TriggerStateChange(_cover, "closed");

        var screen = new ScreenBuilder(_testCtx, _logger, _mqttEntityManager)
            .WithCover(_cover)
            .WithSun(_sun)
            .WithWindSpeedSensor(_windSpeedSensor)
            .Build();

        //Act
        _testCtx.TriggerStateChange(_windSpeedSensor, new EntityState { State = "20" });

        //Assert
        Assert.AreEqual((null, false), screen.StormProtector.DesiredState);
        _testCtx.VerifyScreenGoesUp(_cover, Moq.Times.Never);
        _testCtx.VerifyScreenGoesDown(_cover, Moq.Times.Never);
    }


    [TestMethod]
    public void Rain_Closes_Screen()
    {
        // Arrange
        var screen = new ScreenBuilder(_testCtx, _logger, _mqttEntityManager)
            .WithCover(_cover)
            .WithSun(_sun)
            .WithRainRateSensor(_rainRateSensor)
            .Build();

        //Act
        _testCtx.TriggerStateChange(_rainRateSensor, new EntityState { State = "3" });

        //Assert
        Assert.AreEqual((ScreenState.Down, true), screen.StormProtector.DesiredState);
        _testCtx.VerifyScreenGoesDown(_cover, Moq.Times.Once);
    }

    [TestMethod]
    public void NoRain_DoesNot_Close_Screen()
    {
        // Arrange
        var screen = new ScreenBuilder(_testCtx, _logger, _mqttEntityManager)
            .WithCover(_cover)
            .WithSun(_sun)
            .WithRainRateSensor(_rainRateSensor)
            .Build();

        //Act
        _testCtx.TriggerStateChange(_rainRateSensor, new EntityState { State = "0.8" });

        //Assert
        _testCtx.VerifyScreenGoesDown(_cover, Moq.Times.Never);
    }

    [TestMethod]
    public void NoRain_AfterStorm_Does_Nothing()
    {
        // Arrange
        _testCtx.TriggerStateChange(_rainRateSensor, new EntityState { State = "3" });
        _testCtx.TriggerStateChange(_cover, "closed");

        var screen = new ScreenBuilder(_testCtx, _logger, _mqttEntityManager)
            .WithCover(_cover)
            .WithSun(_sun)
            .WithRainRateSensor(_rainRateSensor)
            .Build();

        //Act
        _testCtx.TriggerStateChange(_rainRateSensor, new EntityState { State = "0" });

        //Assert
        Assert.AreEqual((null, false), screen.StormProtector.DesiredState);
        _testCtx.VerifyScreenGoesUp(_cover, Moq.Times.Never);
        _testCtx.VerifyScreenGoesDown(_cover, Moq.Times.Never);
    }


    [TestMethod]
    public void RainForecast_Closes_Screen()
    {
        // Arrange
        var screen = new ScreenBuilder(_testCtx, _logger, _mqttEntityManager)
            .WithCover(_cover)
            .WithSun(_sun)
            .WithShortTermRainRateSensor(_shortTermRainForecastSensor)
            .Build();

        //Act
        _testCtx.TriggerStateChange(_shortTermRainForecastSensor, new EntityState { State = "0.5" });

        //Assert
        Assert.AreEqual((ScreenState.Down, true), screen.StormProtector.DesiredState);
        _testCtx.VerifyScreenGoesDown(_cover, Moq.Times.Once);
    }

    [TestMethod]
    public void NoRainForecast_DoesNot_Close_Screen()
    {
        // Arrange
        var screen = new ScreenBuilder(_testCtx, _logger, _mqttEntityManager)
            .WithCover(_cover)
            .WithSun(_sun)
            .WithShortTermRainRateSensor(_shortTermRainForecastSensor)
            .Build();

        //Act
        _testCtx.TriggerStateChange(_shortTermRainForecastSensor, new EntityState { State = "0.1" });

        //Assert
        _testCtx.VerifyScreenGoesDown(_cover, Moq.Times.Never);
    }

    [TestMethod]
    public void NoRainForecast_AfterStorm_Does_Nothing()
    {
        _testCtx.TriggerStateChange(_shortTermRainForecastSensor, new EntityState { State = "0.5" });
        _testCtx.TriggerStateChange(_cover, "closed");

        // Arrange
        var screen = new ScreenBuilder(_testCtx, _logger, _mqttEntityManager)
            .WithCover(_cover)
            .WithSun(_sun)
            .WithShortTermRainRateSensor(_shortTermRainForecastSensor)
            .Build();

        //Act
        _testCtx.TriggerStateChange(_shortTermRainForecastSensor, new EntityState { State = "0" });

        //Assert
        Assert.AreEqual((null, false), screen.StormProtector.DesiredState);
        _testCtx.VerifyScreenGoesUp(_cover, Moq.Times.Never);
        _testCtx.VerifyScreenGoesDown(_cover, Moq.Times.Never);
    }

    [TestMethod]
    public void Rain_After_Forecast_Keeps_Screen_Closed()
    {
        // Arrange
        _testCtx.TriggerStateChange(_cover, "open");
        var screen = new ScreenBuilder(_testCtx, _logger, _mqttEntityManager)
            .WithCover(_cover)
            .WithSun(_sun)
            .WithRainRateSensor(_rainRateSensor)
            .WithShortTermRainRateSensor(_shortTermRainForecastSensor)
            .Build();

        _testCtx.TriggerStateChange(_shortTermRainForecastSensor, new EntityState { State = "1" });
        _testCtx.TriggerStateChange(_rainRateSensor, new EntityState { State = "3" });

        //Act
        _testCtx.TriggerStateChange(_shortTermRainForecastSensor, new EntityState { State = "0" });

        //Assert
        Assert.AreEqual((ScreenState.Down, true), screen.StormProtector.DesiredState);
    }

}