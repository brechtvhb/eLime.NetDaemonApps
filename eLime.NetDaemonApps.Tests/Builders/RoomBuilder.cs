using eLime.NetDaemonApps.Config.FlexiLights;
using eLime.NetDaemonApps.Domain.Rooms;
using eLime.NetDaemonApps.Tests.Helpers;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;

namespace eLime.NetDaemonApps.Tests.Builders
{
    public class RoomBuilder
    {
        private readonly AppTestContext _testCtx;
        private readonly ILogger _logger;
        private readonly IMqttEntityManager _mqttEntityManager;

        private RoomConfig _config;

        public RoomBuilder(AppTestContext testCtx, ILogger logger, IMqttEntityManager mqttEntityManager)
        {
            _testCtx = testCtx;
            _logger = logger;
            _mqttEntityManager = mqttEntityManager;
            _config = new RoomConfig
            {
                Name = "Toilet +1",
                Enabled = true,
                MotionSensors = new List<string> { "binary_sensor.motion" },
                OffActions = new List<ActionConfig> { new() { LightAction = LightAction.TurnOff, Light = "light.test" } },
                IgnorePresenceAfterOffDuration = TimeSpan.FromSeconds(5),
                FlexiScenes = new List<FlexiSceneConfig>
                {
                    new()
                    {
                        Name = "default",
                        TurnOffAfterIfTriggeredByMotionSensor = TimeSpan.FromMinutes(5),
                        Actions = new List<ActionConfig> {new () {LightAction = LightAction.TurnOn, Light = "light.test" }}
                    }
                }
            };
        }

        public RoomBuilder WithMultipleActions()
        {
            _config.FlexiScenes = new List<FlexiSceneConfig>
            {
                new()
                {
                    Name = "default",
                    Actions = new List<ActionConfig>
                    {
                        new() {LightAction = LightAction.TurnOn, Light = "light.light1"},
                        new() {LightAction = LightAction.TurnOn, Lights = new List<string> {"light.light2"}},
                        new() {LightAction = LightAction.TurnOn, Lights = new List<string> {"light.light3", "light.light4"}},

                    },
                },
            };

            _config.OffActions = new List<ActionConfig>
            {
                new() {LightAction = LightAction.TurnOff, Light = "light.light1"},
                new() {LightAction = LightAction.TurnOff, Light = "light.light2"},
                new() {LightAction = LightAction.TurnOff, Light = "light.light3"}
            };

            return this;
        }

        public RoomBuilder WithLightColors()
        {
            _config.FlexiScenes = new List<FlexiSceneConfig>
            {
                new()
                {
                    Name = "default",
                    Actions = new List<ActionConfig>
                    {
                        new() {LightAction = LightAction.TurnOn, Light = "light.light1", Color = new() { Name = "red"}},
                        new() {LightAction = LightAction.TurnOn, Light = "light.light2", Color = new() { X = 100, Y = 120 } },
                        new() {LightAction = LightAction.TurnOn, Light = "light.light3", Color = new() { Hue = 100, Saturation = 120 }  },
                        new() {LightAction = LightAction.TurnOn, Light = "light.light4", Color = new() { Kelvin = 2500 }  },
                        new() {LightAction = LightAction.TurnOn, Light = "light.light5", Color = new() { Mireds = 300}  },
                        new() {LightAction = LightAction.TurnOn, Light = "light.light6", Color = new() { Red  = 10, Green = 100, Blue = 50 } },
                        new() {LightAction = LightAction.TurnOn, Light = "light.light7", Color = new() { Red = 100, Green = 200, Blue = 60, White = 255 }  },
                        new() {LightAction = LightAction.TurnOn, Light = "light.light8", Color = new() { Red = 120, Green = 210, Blue = 75, ColdWhite = 50, WarmWhite = 200 }  },
                    },
                },
            };

            _config.OffActions = new List<ActionConfig>
            {
                new() {LightAction = LightAction.TurnOff, Light = "light.light1"},
            };

            return this;
        }
        public RoomBuilder WithLightBrightness()
        {
            _config.FlexiScenes = new List<FlexiSceneConfig>
            {
                new()
                {
                    Name = "default",
                    Actions = new List<ActionConfig>
                    {
                        new() {LightAction = LightAction.TurnOn, Light = "light.light1", Brightness = "100"},
                        new() {LightAction = LightAction.TurnOn, Light = "light.light2", Brightness = "80%"},
                        new() {LightAction = LightAction.TurnOn, Light = "light.light3", Brightness = "+10%"},
                        new() {LightAction = LightAction.TurnOn, Light = "light.light4", Brightness = "-10%"},
                        new() {LightAction = LightAction.TurnOn, Light = "light.light5", Brightness = "+20"},
                        new() {LightAction = LightAction.TurnOn, Light = "light.light6", Brightness = "-20"},
                    },
                },
            };

            _config.OffActions = new List<ActionConfig>
            {
                new() {LightAction = LightAction.TurnOff, Light = "light.light1"},
            };

            return this;
        }

        public RoomBuilder WitFancyLightOptions()
        {
            _config.FlexiScenes = new List<FlexiSceneConfig>
            {
                new()
                {
                    Name = "default",
                    Actions = new List<ActionConfig>
                    {
                        new() {LightAction = LightAction.TurnOn, Light = "light.light1", Effect = "SOHO"},
                        new() {LightAction = LightAction.TurnOn, Light = "light.light2", Flash = "short"},
                    },
                },
            };

            _config.OffActions = new List<ActionConfig>
            {
                new() {LightAction = LightAction.TurnOff, Light = "light.light1"},
            };

            return this;
        }

        public RoomBuilder WithSwitchActions()
        {
            _config.FlexiScenes = new List<FlexiSceneConfig>
            {
                new()
                {
                    Name = "default",
                    Actions = new List<ActionConfig>
                    {
                        new() {SwitchAction = SwitchAction.TurnOn, Switch = "switch.adaptive_lighting"},
                        new() {SwitchAction = SwitchAction.TurnOff, Switch = "switch.inspiration"},
                        new() {SwitchAction = SwitchAction.Pulse, Switch = "switch.triple_click", PulseDuration = TimeSpan.FromMilliseconds(2)},
                    },
                },
            };

            _config.OffActions = new List<ActionConfig>
            {
                new() {LightAction = LightAction.TurnOff, Light = "light.light1"},
            };

            return this;
        }

        public RoomBuilder WithSceneActions()
        {
            _config.FlexiScenes = new List<FlexiSceneConfig>
            {
                new()
                {
                    Name = "default",
                    Actions = new List<ActionConfig>
                    {
                        new() {Scene = "SOHO", TransitionDuration = TimeSpan.FromSeconds(5)}
                    },
                },
            };

            _config.OffActions = new List<ActionConfig>
            {
                new() {LightAction = LightAction.TurnOff, Light = "light.light1"},
            };

            return this;
        }


        public RoomBuilder WithMultipleFlexiScenes()
        {
            _config.FlexiScenes = new List<FlexiSceneConfig>
            {
                new()
                {
                    Name = "morning",
                    Conditions = new List<ConditionConfig> {new() {Binary = "operating_mode.morning"}},
                    Actions = new List<ActionConfig> {new() {LightAction = LightAction.TurnOn, Light = "light.morning", TransitionDuration = TimeSpan.FromSeconds(2), AutoTransitionDuration = TimeSpan.FromSeconds(10)}},
                    TurnOffAfterIfTriggeredByMotionSensor = TimeSpan.FromMinutes(15)
                },
                new()
                {
                    Name = "day",
                    Conditions = new List<ConditionConfig> {new() {Binary = "operating_mode.day"}},
                    Actions = new List<ActionConfig> {new() {LightAction = LightAction.TurnOn, Light = "light.day", TransitionDuration = TimeSpan.FromSeconds(2), AutoTransitionDuration = TimeSpan.FromSeconds(10) } },
                    TurnOffAfterIfTriggeredByMotionSensor = TimeSpan.FromMinutes(5)
                },
                new()
                {
                    Name = "evening",
                    Conditions = new List<ConditionConfig> {new() {Binary = "operating_mode.evening"}},
                    Actions = new List<ActionConfig> {new() {LightAction = LightAction.TurnOn, Light = "light.evening", TransitionDuration = TimeSpan.FromSeconds(2), AutoTransitionDuration = TimeSpan.FromSeconds(10) } },
                    TurnOffAfterIfTriggeredByMotionSensor = TimeSpan.FromMinutes(60)
                }
            };

            _config.OffActions = new List<ActionConfig>
            {
                new() {LightAction = LightAction.TurnOff, Light = "light.morning"},
                new() {LightAction = LightAction.TurnOff, Light = "light.day"},
                new() {LightAction = LightAction.TurnOff, Light = "light.evening"}
            };

            return this;
        }

        public RoomBuilder WithAutoTransition()
        {
            _config.AutoTransition = true;

            return this;
        }

        public Room Build()
        {
            return new Room(_testCtx.HaContext, _logger, _testCtx.Scheduler, _mqttEntityManager, _config);
        }
    }

}
