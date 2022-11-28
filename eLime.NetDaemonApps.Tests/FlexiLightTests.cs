using eLime.NetDaemonApps.Domain.BinarySensors;
using eLime.NetDaemonApps.Domain.Lights;
using eLime.NetDaemonApps.Domain.Rooms;
using eLime.NetDaemonApps.Domain.Scenes;
using eLime.NetDaemonApps.Tests.Builders;
using eLime.NetDaemonApps.Tests.Helpers;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using Moq;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Tests;

[TestClass]
public class FlexiLightTests
{

    private AppTestContext _testCtx;
    private ILogger _logger;
    private IMqttEntityManager _mqttEntityManager;

    [TestInitialize]
    public void Init()
    {
        _testCtx = AppTestContext.Create(DateTime.Now);
        _testCtx.TriggerStateChange(new EnabledSwitch(_testCtx.HaContext, "switch.flexilights_toilet_1"), "on");

        _logger = A.Fake<ILogger<Room>>();
        _mqttEntityManager = A.Fake<IMqttEntityManager>();
    }

    [TestMethod]
    public void Motion_TurnsOn_Light()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).Build();

        //Act
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");

        //Assert
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.test"), Moq.Times.Once);
    }

    [TestMethod]
    public void NoMotion_TurnsOff_Light()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).Build();
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "off");

        //Act
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(6));

        //Assert
        _testCtx.VerifyLightTurnOff(new Light(_testCtx.HaContext, "light.test"), Moq.Times.Once);
    }

    [TestMethod]
    public void Motion_IsIgnored_AfterOff()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).Build();
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "off");
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(3));

        //Act
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");

        //Assert once = first time before turning off
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.test"), Moq.Times.Once);
    }

    [TestMethod]
    public void Motion_IsNotIgnored_AfterOffDuration()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).Build();
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "off");
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(10));

        //Act
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");

        //Assert twice = first time before turning off and second time after having being turned off
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.test"), Moq.Times.Exactly(2));
    }

    [TestMethod]
    public async Task Motion_IsNotIgnored_AfterOffDuration_WhileThereIsStillMotion()
    {
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).Build();
        room.Guard();

        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "off");
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(3));

        //Act
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(3));
        await Task.Delay(1000); //Make sure guard had 1 sec to detect presence is no longer ignored but motion is still active

        //Assert twice = first time before turning off and second time after having being turned off
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.test"), Moq.Times.Exactly(2));
    }

    [TestMethod]
    public void TurnsOn_Correct_FlexiScene()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithMultipleFlexiScenes().Build();
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "operating_mode.day"), "on");

        //Act
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");

        //Assert
        Assert.AreEqual("day", room.FlexiScenes.Current.Name);
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.day"), Moq.Times.Once);
    }

    [TestMethod]
    public void TurnsOn_Correct_FlexiScene_2()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithMultipleFlexiScenes().Build();
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "operating_mode.evening"), "on");

        //Act
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");

        //Assert
        Assert.AreEqual("evening", room.FlexiScenes.Current.Name);
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.evening"), Moq.Times.Once);
    }

    [TestMethod]
    public void TurnsOn_Nothing_IfAllEvaluationsFailed()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithMultipleFlexiScenes().Build();
        //Act
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");

        //Assert
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.morning"), Moq.Times.Never);
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.day"), Moq.Times.Never);
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.evening"), Moq.Times.Never);
    }

    [TestMethod]
    public void AutoTurnOff_WithCorrectDuration()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithMultipleFlexiScenes().Build();
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "operating_mode.day"), "on");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "off");

        //Act
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(6));

        //Assert
        _testCtx.VerifyLightTurnOff(new Light(_testCtx.HaContext, "light.day"), Moq.Times.Once);
    }

    [TestMethod]
    public void AutoTurnOff_WithCorrectDuration_TooEarly()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithMultipleFlexiScenes().Build();
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "operating_mode.day"), "on");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "off");

        //Act
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(1));

        //Assert
        _testCtx.VerifyLightTurnOff(new Light(_testCtx.HaContext, "light.day"), Moq.Times.Never);
    }


    [TestMethod]
    public async Task AutoTransition_WithCorrectDuration()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithMultipleFlexiScenes().WithAutoTransition().Build();
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "operating_mode.day"), "on");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "off");

        //Act
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "operating_mode.day"), "off");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "operating_mode.evening"), "on");

        await Task.Delay(1100); //allow debounce to bounce

        //Assert
        Assert.AreEqual("evening", room.FlexiScenes.Current.Name);
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.evening"), new LightTurnOnParameters { Transition = 10 }, Moq.Times.Once);
    }


    [TestMethod]
    public void Motion_RunsAllActions()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithMultipleActions().Build();

        //Act
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");

        //Assert
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.light1"), Moq.Times.Once);
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.light2"), Moq.Times.Once);
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.light3"), Moq.Times.Once);
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.light4"), Moq.Times.Once);
    }

    [TestMethod]
    public void Service_Calls_WithCorrectLightColorParameters()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithLightColors().Build();

        //Act
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");

        //Assert
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.light1"), new LightTurnOnParameters { ColorName = "red" }, Moq.Times.Once);
        _testCtx.HaContextMock.Verify(c => c.CallService("light", "turn_on", It.Is<ServiceTarget>(x => x.EntityIds != null && x.EntityIds.Any(id => id == "light.light2")), It.Is<LightTurnOnParameters>(x => (x.XyColor as List<int>).First() == 100 && (x.XyColor as List<int>).Last() == 120)), Moq.Times.Once);
        _testCtx.HaContextMock.Verify(c => c.CallService("light", "turn_on", It.Is<ServiceTarget>(x => x.EntityIds != null && x.EntityIds.Any(id => id == "light.light3")), It.Is<LightTurnOnParameters>(x => (x.HsColor as List<int>).First() == 100 && (x.HsColor as List<int>).Last() == 120)), Moq.Times.Once);
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.light4"), new LightTurnOnParameters { Kelvin = 2500 }, Moq.Times.Once);
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.light5"), new LightTurnOnParameters { ColorTemp = 300 }, Moq.Times.Once);
        _testCtx.HaContextMock.Verify(c => c.CallService("light", "turn_on", It.Is<ServiceTarget>(x => x.EntityIds != null && x.EntityIds.Any(id => id == "light.light6")), It.Is<LightTurnOnParameters>(x => (x.RgbColor as List<int>).First() == 10 && (x.RgbColor as List<int>).Skip(1).First() == 100 && (x.RgbColor as List<int>).Last() == 50)), Moq.Times.Once);
        _testCtx.HaContextMock.Verify(c => c.CallService("light", "turn_on", It.Is<ServiceTarget>(x => x.EntityIds != null && x.EntityIds.Any(id => id == "light.light7")), It.Is<LightTurnOnParameters>(x => (x.RgbwColor as List<int>).First() == 100 && (x.RgbwColor as List<int>).Skip(1).First() == 200 && (x.RgbwColor as List<int>).Skip(2).First() == 60 && (x.RgbwColor as List<int>).Last() == 255)), Moq.Times.Once);
        _testCtx.HaContextMock.Verify(c => c.CallService("light", "turn_on", It.Is<ServiceTarget>(x => x.EntityIds != null && x.EntityIds.Any(id => id == "light.light8")), It.Is<LightTurnOnParameters>(x => (x.RgbwwColor as List<int>).First() == 120 && (x.RgbwwColor as List<int>).Skip(1).First() == 210 && (x.RgbwwColor as List<int>).Skip(2).First() == 75 && (x.RgbwwColor as List<int>).Skip(3).First() == 50 && (x.RgbwwColor as List<int>).Last() == 200)), Moq.Times.Once);
    }

    [TestMethod]
    public void Service_Calls_WithCorrectLightBrightnessParameters()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithLightBrightness().Build();

        //Act
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");

        //Assert
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.light1"), new LightTurnOnParameters { Brightness = 100 }, Moq.Times.Once);
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.light2"), new LightTurnOnParameters { BrightnessPct = 80 }, Moq.Times.Once);
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.light3"), new LightTurnOnParameters { BrightnessStepPct = 10 }, Moq.Times.Once);
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.light4"), new LightTurnOnParameters { BrightnessStepPct = -10 }, Moq.Times.Once);
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.light5"), new LightTurnOnParameters { BrightnessStep = 20 }, Moq.Times.Once);
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.light6"), new LightTurnOnParameters { BrightnessStep = -20 }, Moq.Times.Once);
    }

    [TestMethod]
    public void Service_Calls_WithCorrectFancyLightParameters()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WitFancyLightOptions().Build();

        //Act
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");

        //Assert
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.light1"), new LightTurnOnParameters { Effect = "SOHO" }, Moq.Times.Once);
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.light2"), new LightTurnOnParameters { Flash = "short" }, Moq.Times.Once);
    }

    [TestMethod]
    public async Task Service_Calls_CorrectSwitchActions()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithSwitchActions().Build();

        //Act
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");
        await Task.Delay(10);

        //Assert
        _testCtx.VerifySwitchTurnOn(new Switch(_testCtx.HaContext, "switch.adaptive_lighting"), Moq.Times.Once);
        _testCtx.VerifySwitchTurnOff(new Switch(_testCtx.HaContext, "switch.inspiration"), Moq.Times.Once);

        _testCtx.VerifySwitchTurnOn(new Switch(_testCtx.HaContext, "switch.triple_click"), Moq.Times.Once);
        _testCtx.VerifySwitchTurnOff(new Switch(_testCtx.HaContext, "switch.triple_click"), Moq.Times.Once);
    }

    [TestMethod]
    public async Task Service_Calls_CorrectScene()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithSceneActions().Build();

        //Act
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");
        await Task.Delay(10);

        //Assert
        _testCtx.VerifySceneTurnOn(new Scene(_testCtx.HaContext, "SOHO"), new SceneTurnOnParameters { Transition = 5 }, Moq.Times.Once);
    }
}