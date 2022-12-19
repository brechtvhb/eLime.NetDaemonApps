using eLime.NetDaemonApps.Domain.BinarySensors;
using eLime.NetDaemonApps.Domain.Lights;
using eLime.NetDaemonApps.Domain.NumericSensors;
using eLime.NetDaemonApps.Domain.Rooms;
using eLime.NetDaemonApps.Domain.Scenes;
using eLime.NetDaemonApps.Domain.TextSensors;
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
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_day"), "on");

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
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_evening"), "on");

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
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_day"), "on");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "off");

        //Act
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(6));

        //Assert
        _testCtx.VerifyLightTurnOff(new Light(_testCtx.HaContext, "light.day"), Moq.Times.Once);
    }

    [TestMethod]
    public void DoNotAutoTurnOff_WhenTooEarly()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithMultipleFlexiScenes().Build();
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_day"), "on");
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
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_day"), "on");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "off");
        await Task.Delay(50); //allow debounce to bounce

        //Act
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_day"), "off");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_evening"), "on");
        await Task.Delay(50); //allow debounce to bounce

        //Assert
        Assert.AreEqual("evening", room.FlexiScenes.Current.Name);
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.evening"), new LightTurnOnParameters { Transition = 10 }, Moq.Times.Once);
    }

    [TestMethod]
    public async Task AutoTransition_TurnOffIfNoValidSceneFound()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithMultipleFlexiScenes().WithAutoTransition().WithAutoTransitionTurnOfIfNoValidSceneFound().Build();
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_day"), "on");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "off");
        await Task.Delay(50); //allow debounce to bounce

        //Act
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_day"), "off");
        await Task.Delay(50); //allow debounce to bounce

        //Assert
        Assert.IsNull(room.FlexiScenes.Current);
        _testCtx.VerifyLightTurnOff(new Light(_testCtx.HaContext, "light.day"), Moq.Times.Once);
    }


    [TestMethod]
    public async Task AutoTransition_DoNotTurnOffIfNoValidSceneFoundWhenNotConfigured()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithMultipleFlexiScenes().WithAutoTransition().Build();
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_day"), "on");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "off");
        await Task.Delay(50); //allow debounce to bounce

        //Act
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_day"), "off");
        await Task.Delay(50); //allow debounce to bounce

        //Assert
        Assert.AreEqual("day", room.FlexiScenes.Current.Name);
        _testCtx.VerifyLightTurnOff(new Light(_testCtx.HaContext, "light.day"), Moq.Times.Never);
    }

    [TestMethod]
    public async Task FullyAutomated_Turns_on()
    {
        //Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).FullyAutomated().WithMultipleFlexiScenes().Build();

        //Act
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_day"), "on");
        await Task.Delay(50); //allow debounce to bounce

        //Assert
        Assert.IsNotNull(room.FlexiScenes.Current);
        Assert.IsNull(room.TurnOffAt);
        Assert.AreEqual(InitiatedBy.FullyAutomated, room.InitiatedBy);
        Assert.AreEqual("day", room.FlexiScenes.Current.Name);
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.day"), Moq.Times.Once);
    }

    [TestMethod]
    public async Task FullyAutomated_Transitions()
    {
        //Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).FullyAutomated().WithMultipleFlexiScenes().Build();
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_day"), "on");
        await Task.Delay(50); //allow debounce to bounce

        //Act
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_evening"), "on");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_day"), "off");
        await Task.Delay(50); //allow debounce to bounce

        //Assert
        Assert.IsNotNull(room.FlexiScenes.Current);
        Assert.AreEqual("evening", room.FlexiScenes.Current.Name);
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.evening"), Moq.Times.Once);
    }

    [TestMethod]
    public async Task FullyAutomated_Turns_Off_IfConfigured()
    {
        //Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).FullyAutomated().WithAutoTransitionTurnOfIfNoValidSceneFound().WithMultipleFlexiScenes().Build();
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_day"), "on");
        await Task.Delay(50); //allow debounce to bounce

        //Act
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_day"), "off");
        await Task.Delay(50); //allow debounce to bounce

        //Assert
        Assert.IsNull(room.FlexiScenes.Current);
        _testCtx.VerifyLightTurnOff(new Light(_testCtx.HaContext, "light.day"), Moq.Times.Once);
    }

    [TestMethod]
    public async Task FullyAutomated_Does_Not_Turn_Off_If_Not_Configured()
    {
        //Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).FullyAutomated().WithMultipleFlexiScenes().Build();
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_day"), "on");
        await Task.Delay(50); //allow debounce to bounce

        //Act
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_day"), "off");
        await Task.Delay(50); //allow debounce to bounce

        //Assert
        Assert.IsNotNull(room.FlexiScenes.Current);
        Assert.AreEqual("day", room.FlexiScenes.Current.Name);
        _testCtx.VerifyLightTurnOff(new Light(_testCtx.HaContext, "light.day"), Moq.Times.Never);
    }

    [TestMethod]
    public void FullyAutomated_Throws_Exception_If_No_Scene_With_Conditions()
    {
        //Arrange
        Exception exception = null;

        // Act
        try
        {
            var _ = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).FullyAutomated().Build();
        }
        catch (Exception ex) { exception = ex; }

        //Assert
        Assert.IsNotNull(exception);
        Assert.IsTrue(exception.Message.Contains("Define at least one flexi scene with conditions"));
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
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.light1"), new LightTurnOnParameters { Transition = 2, ColorName = "red" }, Moq.Times.Once);
        _testCtx.HaContextMock.Verify(c => c.CallService("light", "turn_on", It.Is<ServiceTarget>(x => x.EntityIds != null && x.EntityIds.Any(id => id == "light.light2")), It.Is<LightTurnOnParameters>(x => (x.XyColor as List<int>).First() == 100 && (x.XyColor as List<int>).Last() == 120)), Moq.Times.Once);
        _testCtx.HaContextMock.Verify(c => c.CallService("light", "turn_on", It.Is<ServiceTarget>(x => x.EntityIds != null && x.EntityIds.Any(id => id == "light.light3")), It.Is<LightTurnOnParameters>(x => (x.HsColor as List<int>).First() == 100 && (x.HsColor as List<int>).Last() == 120)), Moq.Times.Once);
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.light4"), new LightTurnOnParameters { Transition = 2, Kelvin = 2500 }, Moq.Times.Once);
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.light5"), new LightTurnOnParameters { Transition = 2, ColorTemp = 300 }, Moq.Times.Once);
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
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.light1"), new LightTurnOnParameters { Transition = 2, Brightness = 100 }, Moq.Times.Once);
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.light2"), new LightTurnOnParameters { Transition = 2, BrightnessPct = 80 }, Moq.Times.Once);
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.light3"), new LightTurnOnParameters { Transition = 2, BrightnessStepPct = 10 }, Moq.Times.Once);
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.light4"), new LightTurnOnParameters { Transition = 2, BrightnessStepPct = -10 }, Moq.Times.Once);
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.light5"), new LightTurnOnParameters { Transition = 2, BrightnessStep = 20 }, Moq.Times.Once);
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.light6"), new LightTurnOnParameters { Transition = 2, BrightnessStep = -20 }, Moq.Times.Once);
    }

    [TestMethod]
    public void Service_Calls_WithCorrectFancyLightParameters()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WitFancyLightOptions().Build();

        //Act
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");

        //Assert
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.light1"), new LightTurnOnParameters { Transition = 2, Effect = "SOHO" }, Moq.Times.Once);
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.light2"), new LightTurnOnParameters { Transition = 2, Flash = "short" }, Moq.Times.Once);
    }

    [TestMethod]
    public async Task Service_Calls_CorrectSwitchActions()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithSwitchActions().Build();

        //Act
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");

        await Task.Delay(10); //Allow pulse to pulse

        //Assert
        _testCtx.VerifySwitchTurnOn(new BinarySwitch(_testCtx.HaContext, "switch.adaptive_lighting"), Moq.Times.Once);
        _testCtx.VerifySwitchTurnOff(new BinarySwitch(_testCtx.HaContext, "switch.inspiration"), Moq.Times.Once);

        _testCtx.VerifySwitchTurnOn(new BinarySwitch(_testCtx.HaContext, "switch.triple_click"), Moq.Times.Once);
        _testCtx.VerifySwitchTurnOff(new BinarySwitch(_testCtx.HaContext, "switch.triple_click"), Moq.Times.Once);
    }

    [TestMethod]
    public void Service_Calls_CorrectScene()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithSceneActions().Build();

        //Act
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");

        //Assert
        _testCtx.VerifySceneTurnOn(new Scene(_testCtx.HaContext, "scene.SOHO"), new SceneTurnOnParameters { Transition = 5 }, Moq.Times.Once);
    }

    [TestMethod]
    public void Complex_Conditions_NotSoComplex()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithComplexConditions().Build();
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_evening"), "on");

        //Act
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");

        //Assert
        _testCtx.VerifySceneTurnOn(new Scene(_testCtx.HaContext, "scene.evening"), Moq.Times.Once);
    }

    [TestMethod]
    public void Complex_Conditions_And()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithComplexConditions().Build();
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_evening"), "on");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.watching_tv"), "on");

        //Act
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");

        //Assert
        _testCtx.VerifySceneTurnOn(new Scene(_testCtx.HaContext, "scene.tv"), Moq.Times.Once);
    }

    [TestMethod]
    public void Complex_Conditions_And_FirstTakesPrecedence()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithComplexConditions().Build();
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_evening"), "on");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.watching_tv"), "on");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_party"), "on");

        //Act
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");

        //Assert
        _testCtx.VerifySceneTurnOn(new Scene(_testCtx.HaContext, "scene.party"), Moq.Times.Once);
    }

    [TestMethod]
    public void Complex_Conditions_NoAdvent_DoesNotTurnOnChristmasTree()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithComplexConditions().Build();
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_morning"), "on");

        //Act
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");

        //Assert
        _testCtx.VerifySceneTurnOn(new Scene(_testCtx.HaContext, "scene.morning"), Moq.Times.Once);
    }

    [TestMethod]
    public void Complex_Conditions_Advent_TurnsOnChristmasTree()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithComplexConditions().Build();
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_morning"), "on");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_advent"), "on");

        //Act
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");

        //Assert
        _testCtx.VerifySceneTurnOn(new Scene(_testCtx.HaContext, "scene.morning"), Moq.Times.Once);
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.christmas_tree"), Moq.Times.Once);
    }

    [TestMethod]
    public void Complex_Conditions_Advent_Morning_Vacation_MccallisterizesHouse()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithComplexConditions().Build();
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_morning"), "on");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_advent"), "on");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_vacation"), "on");

        //Act
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");

        //Assert
        _testCtx.VerifySceneTurnOn(new Scene(_testCtx.HaContext, "scene.kevin_mccallister"), Moq.Times.Once);
    }

    [TestMethod]
    public void Complex_Conditions_Advent_Evening_Vacation_MccallisterizesHouse()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithComplexConditions().Build();
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_evening"), "on");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_advent"), "on");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_vacation"), "on");

        //Act
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");

        //Assert
        _testCtx.VerifySceneTurnOn(new Scene(_testCtx.HaContext, "scene.kevin_mccallister"), Moq.Times.Once);
    }


    [TestMethod]
    public void Complex_Conditions_Advent_Day_Vacation_DoesNotMccallisterizesHouse()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithComplexConditions().Build();
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_day"), "on");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_advent"), "on");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_vacation"), "on");

        //Act
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");

        //Assert
        _testCtx.VerifySceneTurnOn(new Scene(_testCtx.HaContext, "scene.kevin_mccallister"), Moq.Times.Never);
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.day"), Moq.Times.Once);
    }

    [TestMethod]
    public void Complex_Conditions_Negative()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithNegativeCondition().Build();
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_morning"), "off");

        //Act
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");

        //Assert
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.day"), Moq.Times.Once);
    }

    [TestMethod]
    public void Complex_And_Or_Binary()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithAllKindsOfConditions().Build();
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_night"), "off");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_evening"), "on");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_peak_hour_morning"), "off");

        //Act
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");

        //Assert
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.night"), Moq.Times.Once);
    }


    [TestMethod]
    public void DoesNotActivateLight_If_IlluminanceToHigh()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithIlluminanceSensor().Build();
        _testCtx.TriggerStateChange(new IlluminanceSensor(_testCtx.HaContext, "sensor.illuminance"), "50");

        //Act
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");

        //Assert
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.test"), Moq.Times.Never);
    }

    [TestMethod]
    public void DoesActivateLight_If_IlluminanceOk()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithIlluminanceSensor().Build();
        _testCtx.TriggerStateChange(new IlluminanceSensor(_testCtx.HaContext, "sensor.illuminance"), "5");

        //Act
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");

        //Assert
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.test"), Moq.Times.Once);
    }

    [TestMethod]
    public void DoesActivateLight_If_OneOfIlluminanceOk()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithIlluminanceSensors().Build();
        _testCtx.TriggerStateChange(new IlluminanceSensor(_testCtx.HaContext, "sensor.illuminance1"), "50");
        _testCtx.TriggerStateChange(new IlluminanceSensor(_testCtx.HaContext, "sensor.illuminance2"), "5");

        //Act
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");

        //Assert
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.test"), Moq.Times.Once);
    }

    [TestMethod]
    public void DoesAutoSwitchOff_If_BothAboveIlluminance()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithIlluminanceSensors().Build();
        _testCtx.TriggerStateChange(new IlluminanceSensor(_testCtx.HaContext, "sensor.illuminance1"), "50");
        _testCtx.TriggerStateChange(new IlluminanceSensor(_testCtx.HaContext, "sensor.illuminance2"), "5");

        //Act
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");
        _testCtx.TriggerStateChange(new IlluminanceSensor(_testCtx.HaContext, "sensor.illuminance2"), "50");

        //Assert
        _testCtx.VerifyLightTurnOff(new Light(_testCtx.HaContext, "light.test"), Moq.Times.Once);
    }

    [TestMethod]
    public void DoesNotAutoSwitchOff_If_NotBothAboveIlluminance()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithIlluminanceSensors().Build();
        _testCtx.TriggerStateChange(new IlluminanceSensor(_testCtx.HaContext, "sensor.illuminance1"), "50");
        _testCtx.TriggerStateChange(new IlluminanceSensor(_testCtx.HaContext, "sensor.illuminance2"), "5");

        //Act
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");
        _testCtx.TriggerStateChange(new IlluminanceSensor(_testCtx.HaContext, "sensor.illuminance1"), "10");
        _testCtx.TriggerStateChange(new IlluminanceSensor(_testCtx.HaContext, "sensor.illuminance2"), "100");

        //Assert
        _testCtx.VerifyLightTurnOff(new Light(_testCtx.HaContext, "light.test"), Moq.Times.Never);
    }

    [TestMethod]
    public async Task Click_Triggers_FlexiScene()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithSwitch().WithMultipleFlexiScenes().Build();
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_day"), "on");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");

        //Act
        _testCtx.SimulateClick(new StateSwitch(_testCtx.HaContext, "binary_sensor.switch"));
        await Task.Delay(30); //allow debounce to ... debounce

        //Assert
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.day"), Moq.Times.Once);
    }


    [TestMethod]
    public void Click_Extends_AutoTurnOff_Duration()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithSwitch().WithMultipleFlexiScenes().Build();
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_day"), "on");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");

        //Act
        _testCtx.SimulateClick(new StateSwitch(_testCtx.HaContext, "sensor.switch"));

        //Assert
        Assert.IsTrue(room.TurnOffAt.Value > DateTime.Now.AddHours(3));
    }


    [TestMethod]
    public void Click_DoesNotTrigger_NextScene()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithSwitch().WithMultipleFlexiScenes().Build();
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_day"), "on");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");

        //Act
        _testCtx.SimulateClick(new StateSwitch(_testCtx.HaContext, "sensor.switch"));

        //Assert
        Assert.AreEqual("day", room.FlexiScenes.Current.Name);
        Assert.AreEqual(InitiatedBy.Switch, room.InitiatedBy);
    }

    [TestMethod]
    public void Click_DoesTrigger_NextScene_After_Two_Clicks()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithSwitch().WithMultipleFlexiScenes().Build();
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_day"), "on");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");

        //Act
        _testCtx.SimulateClick(new StateSwitch(_testCtx.HaContext, "sensor.switch"));
        _testCtx.SimulateClick(new StateSwitch(_testCtx.HaContext, "sensor.switch"));

        //Assert
        Assert.AreEqual("evening", room.FlexiScenes.Current.Name);
        Assert.AreEqual(InitiatedBy.Switch, room.InitiatedBy);
    }


    [TestMethod]
    public void Click_DoesTrigger_NextScene_IfBehaviourSet()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithSwitch().WithSwitchChangeOFfDurationAndGoToNextAutomationAtInitialOnClickAfterMotion().WithMultipleFlexiScenes().Build();
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_day"), "on");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");

        //Act
        _testCtx.SimulateClick(new StateSwitch(_testCtx.HaContext, "sensor.switch"));

        //Assert
        Assert.AreEqual("evening", room.FlexiScenes.Current.Name);
        Assert.AreEqual(InitiatedBy.Switch, room.InitiatedBy);
    }

    [TestMethod]
    public void Click_DoesTrigger_NextScene_WithLimitedOptions()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithSwitch().WithSwitchChangeOFfDurationAndGoToNextAutomationAtInitialOnClickAfterMotion().WithMultipleFlexiScenesLimitedNext().Build();
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_day"), "on");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");

        //Act
        _testCtx.SimulateClick(new StateSwitch(_testCtx.HaContext, "sensor.switch"));

        //Assert
        Assert.AreEqual("morning", room.FlexiScenes.Current.Name);
        Assert.AreEqual("day", room.FlexiScenes.Initial.Name);
        Assert.AreEqual(InitiatedBy.Switch, room.InitiatedBy);
    }

    [TestMethod]
    public void Click_DoesTrigger_NextScene_WithLimitedOptions_KeepsWorkingAfterMultipleClicks()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithSwitch().WithSwitchChangeOFfDurationAndGoToNextAutomationAtInitialOnClickAfterMotion().WithMultipleFlexiScenesLimitedNext().Build();
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_day"), "on");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");

        //Act
        _testCtx.SimulateClick(new StateSwitch(_testCtx.HaContext, "sensor.switch"));
        _testCtx.SimulateClick(new StateSwitch(_testCtx.HaContext, "sensor.switch"));

        //Assert
        Assert.AreEqual("day", room.FlexiScenes.Current.Name);
        Assert.AreEqual("day", room.FlexiScenes.Initial.Name);
        Assert.AreEqual(InitiatedBy.Switch, room.InitiatedBy);
    }

    [TestMethod]
    public void Click_DoesTrigger_NextScene_WithLimitedOptions_KeepsWorkingAfterManyClicks()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithSwitch().WithSwitchChangeOFfDurationAndGoToNextAutomationAtInitialOnClickAfterMotion().WithMultipleFlexiScenesLimitedNext().Build();
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_day"), "on");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");

        //Act
        _testCtx.SimulateClick(new StateSwitch(_testCtx.HaContext, "sensor.switch"));
        _testCtx.SimulateClick(new StateSwitch(_testCtx.HaContext, "sensor.switch"));
        _testCtx.SimulateClick(new StateSwitch(_testCtx.HaContext, "sensor.switch"));

        //Assert
        Assert.AreEqual("morning", room.FlexiScenes.Current.Name);
        Assert.AreEqual("day", room.FlexiScenes.Initial.Name);
        Assert.AreEqual(InitiatedBy.Switch, room.InitiatedBy);
    }


    [TestMethod]
    public void Click_DoesTrigger_CyclesTo_FirstScene_IfLast()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithSwitch().WithSwitchChangeOFfDurationAndGoToNextAutomationAtInitialOnClickAfterMotion().WithMultipleFlexiScenes().Build();
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_evening"), "on");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");

        //Act
        _testCtx.SimulateClick(new StateSwitch(_testCtx.HaContext, "sensor.switch"));

        //Assert
        Assert.AreEqual("morning", room.FlexiScenes.Current.Name);
        Assert.AreEqual(InitiatedBy.Switch, room.InitiatedBy);
    }

    [TestMethod]
    public void DoubleClick_Triggers_DoubleClickActions()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithSwitch().WithMultipleFlexiScenes().Build();
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_day"), "on");

        //Act
        _testCtx.SimulateDoubleClick(new StateSwitch(_testCtx.HaContext, "sensor.switch"));

        //Assert
        _testCtx.VerifyLightTurnOff(new Light(_testCtx.HaContext, "light.day"), Moq.Times.Once);
    }

    [TestMethod]
    public void TripleClick_Triggers_TripleClickActions()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithSwitch().WithMultipleFlexiScenes().Build();
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_day"), "on");

        //Act
        _testCtx.SimulateTripleClick(new StateSwitch(_testCtx.HaContext, "sensor.switch"));
        //Assert
        _testCtx.VerifyLightTurnOff(new Light(_testCtx.HaContext, "light.evening"), Moq.Times.Once);
    }

    [TestMethod]
    public void LongClick_Triggers_LongClickActions()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithSwitch().WithMultipleFlexiScenes().Build();
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_day"), "on");

        //Act
        _testCtx.SimulateLongClick(new StateSwitch(_testCtx.HaContext, "sensor.switch"));

        //Assert
        _testCtx.VerifyLightTurnOff(new Light(_testCtx.HaContext, "light.morning"), Moq.Times.Once);
    }

    [TestMethod]
    public void UberLongClick_Triggers_LongClickActions_IfNoUberLongClickActionsDefined()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithSwitch().WithMultipleFlexiScenes().Build();
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_day"), "on");

        //Act
        _testCtx.SimulateUberLongClick(new StateSwitch(_testCtx.HaContext, "sensor.switch"));

        //Assert
        _testCtx.VerifyLightTurnOff(new Light(_testCtx.HaContext, "light.morning"), Moq.Times.Once);
    }

    [TestMethod]
    public void UberLongClick_Triggers_UberLongClickActions()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithSwitch().WithUberLongClickActions().WithMultipleFlexiScenes().Build();
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_day"), "on");

        //Act
        _testCtx.SimulateUberLongClick(new StateSwitch(_testCtx.HaContext, "sensor.switch"));

        //Assert
        _testCtx.VerifyLightTurnOff(new Light(_testCtx.HaContext, "light.morning"), Moq.Times.Once);
        _testCtx.VerifyLightTurnOff(new Light(_testCtx.HaContext, "light.day"), Moq.Times.Once);
        _testCtx.VerifyLightTurnOff(new Light(_testCtx.HaContext, "light.evening"), Moq.Times.Once);
    }

    [TestMethod]
    public void OffInput_Triggers_OffActions()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithMultipleFlexiScenes().WithOffSensor().Build();
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.operating_mode_day"), "on");

        //Act
        _testCtx.TriggerStateChange(new BinarySensor(_testCtx.HaContext, "binary_sensor.triple_click"), "on");

        //Assert
        _testCtx.VerifyLightTurnOff(new Light(_testCtx.HaContext, "light.morning"), Moq.Times.Once);
        _testCtx.VerifyLightTurnOff(new Light(_testCtx.HaContext, "light.day"), Moq.Times.Once);
        _testCtx.VerifyLightTurnOff(new Light(_testCtx.HaContext, "light.evening"), Moq.Times.Once);
    }

    [TestMethod]
    public void OffAction_InFlexiScene_Triggers_OffActions()
    {
        // Arrange
        var room = new RoomBuilder(_testCtx, _logger, _mqttEntityManager).WithOffActionInFlexiScene().WithSwitch().Build();
        _testCtx.SimulateClick(new StateSwitch(_testCtx.HaContext, "sensor.switch"));

        //Act
        _testCtx.SimulateClick(new StateSwitch(_testCtx.HaContext, "sensor.switch"));
        //Assert
        _testCtx.VerifyLightTurnOff(new Light(_testCtx.HaContext, "light.light1"), Moq.Times.Once);
        Assert.IsNull(room.FlexiScenes.Current);
        Assert.AreEqual(InitiatedBy.NoOne, room.InitiatedBy);
    }
}