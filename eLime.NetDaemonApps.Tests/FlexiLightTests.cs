using eLime.NetDaemonApps.Config.FlexiLights;
using eLime.NetDaemonApps.Domain.BinarySensors;
using eLime.NetDaemonApps.Domain.Lights;
using eLime.NetDaemonApps.Domain.Rooms;
using eLime.NetDaemonApps.Tests.Helpers;
using FakeItEasy;
using Microsoft.Extensions.Logging;

namespace eLime.NetDaemonApps.Tests;

[TestClass]
public class FlexiLightTests
{

    private AppTestContext _testCtx;
    private ILogger _logger;
    [TestInitialize]
    public void Init()
    {
        _testCtx = AppTestContext.Create(DateTime.Now);
        _logger = A.Fake<ILogger<Room>>();

    }

    [TestMethod]
    public void Motion_TurnsOn_Light()
    {
        // Arrange
        var roomConfig = new RoomConfig
        {
            Name = "Toilet +1",
            Enabled = true,
            MotionSensors = new List<string> { "binary_sensor.motion" },
            OffActions = new List<ActionConfig> { new() { LightAction = LightAction.TurnOff, Lights = new List<string> { "light.test" } } },
            FlexiScenes = new List<FlexiSceneConfig>
            {
                new()
                {
                    Name = "evening",
                    Conditions = new List<ConditionConfig> {new () {Binary = "operating_mode.evening"}},
                    Actions = new List<ActionConfig> {new () {LightAction = LightAction.TurnOn, Lights = new List<string> {"light.test"}}}
                }
            }
        };
        var _ = new Room(_testCtx.HaContext, _logger, _testCtx.Scheduler, roomConfig);
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "operating_mode.evening"), "on");

        //Act
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");

        //Assert
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.test"), Moq.Times.Once);
    }

    [TestMethod]
    public void NoMotion_TurnsOff_Light()
    {
        // Arrange
        var roomConfig = new RoomConfig
        {
            Name = "Toilet +1",
            Enabled = true,
            MotionSensors = new List<string> { "binary_sensor.motion" },
            OffActions = new List<ActionConfig> { new() { LightAction = LightAction.TurnOff, Lights = new List<string> { "light.test" } } },
            FlexiScenes = new List<FlexiSceneConfig>
            {
                new()
                {
                    Name = "evening",
                    TurnOffAfterIfTriggeredByMotionSensor = TimeSpan.FromMinutes(5),
                    Conditions = new List<ConditionConfig> {new () {Binary = "operating_mode.evening"}},
                    Actions = new List<ActionConfig> {new () {LightAction = LightAction.TurnOn, Lights = new List<string> {"light.test"}}}
                }
            }
        };
        var _ = new Room(_testCtx.HaContext, _logger, _testCtx.Scheduler, roomConfig);
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "operating_mode.evening"), "on");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");

        //Act
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "off");
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(6));

        //Assert
        _testCtx.VerifyLightTurnOff(new Light(_testCtx.HaContext, "light.test"), Moq.Times.Once);
    }

    [TestMethod]
    public void Motion_IsIgnored_AfterOff()
    {
        // Arrange
        var roomConfig = new RoomConfig
        {
            Name = "Toilet +1",
            Enabled = true,
            MotionSensors = new List<string> { "binary_sensor.motion" },
            OffActions = new List<ActionConfig> { new() { LightAction = LightAction.TurnOff, Lights = new List<string> { "light.test" } } },
            IgnorePresenceAfterOffDuration = TimeSpan.FromSeconds(5),
            FlexiScenes = new List<FlexiSceneConfig>
            {
                new()
                {
                    Name = "evening",
                    TurnOffAfterIfTriggeredByMotionSensor = TimeSpan.FromMinutes(5),
                    Conditions = new List<ConditionConfig> {new () {Binary = "operating_mode.evening"}},
                    Actions = new List<ActionConfig> {new () {LightAction = LightAction.TurnOn, Lights = new List<string> {"light.test"}}}
                }
            }
        };
        var _ = new Room(_testCtx.HaContext, _logger, _testCtx.Scheduler, roomConfig);
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "operating_mode.evening"), "on");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "off");
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(5));
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(3));

        //Act
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");

        //Assert once = first time before turning off
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.test"), Moq.Times.Once);
    }

    [TestMethod]
    public void Motion_IsNotIgnored_AfterOffDuration()
    {
        // Arrange
        var roomConfig = new RoomConfig
        {
            Name = "Toilet +1",
            Enabled = true,
            MotionSensors = new List<string> { "binary_sensor.motion" },
            OffActions = new List<ActionConfig> { new() { LightAction = LightAction.TurnOff, Lights = new List<string> { "light.test" } } },
            IgnorePresenceAfterOffDuration = TimeSpan.FromSeconds(5),
            FlexiScenes = new List<FlexiSceneConfig>
            {
                new()
                {
                    Name = "evening",
                    TurnOffAfterIfTriggeredByMotionSensor = TimeSpan.FromMinutes(5),
                    Conditions = new List<ConditionConfig> {new () {Binary = "operating_mode.evening"}},
                    Actions = new List<ActionConfig> {new () {LightAction = LightAction.TurnOn, Lights = new List<string> {"light.test"}}}
                }
            }
        };
        var _ = new Room(_testCtx.HaContext, _logger, _testCtx.Scheduler, roomConfig);
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "operating_mode.evening"), "on");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "off");
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(5));
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(10));

        //Act
        _testCtx.TriggerStateChange(new MotionSensor(_testCtx.HaContext, "binary_sensor.motion"), "on");

        //Assert twice = first time before turning off and second time after having being turned off
        _testCtx.VerifyLightTurnOn(new Light(_testCtx.HaContext, "light.test"), Moq.Times.Exactly(2));
    }
}