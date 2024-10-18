using eLime.NetDaemonApps.Config.FlexiLights;
using eLime.NetDaemonApps.Domain.FlexiScenes.Rooms;
using eLime.NetDaemonApps.Domain.Storage;
using eLime.NetDaemonApps.Tests.Helpers;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using InitialClickAfterMotionBehaviour = eLime.NetDaemonApps.Config.FlexiLights.InitialClickAfterMotionBehaviour;

namespace eLime.NetDaemonApps.Tests.Builders
{
    public class RoomBuilder
    {
        private readonly AppTestContext _testCtx;
        private readonly ILogger _logger;
        private readonly IMqttEntityManager _mqttEntityManager;
        private readonly IFileStorage _fileStorage;

        private RoomConfig _config;

        public RoomBuilder(AppTestContext testCtx, ILogger logger, IMqttEntityManager mqttEntityManager, IFileStorage fileStorage, String? name = null)
        {
            _testCtx = testCtx;
            _logger = logger;
            _mqttEntityManager = mqttEntityManager;
            _fileStorage = fileStorage;
            _config = new RoomConfig
            {
                Name = name ?? "Toilet +1",
                Enabled = true,
                MotionSensors = new List<MotionSensorConfig> { new MotionSensorConfig { Sensor = "binary_sensor.motion", MixinScene = null } },
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
            _config.FlexiScenes =
            [
                new()
                {
                    Name = "default",
                    Actions =
                    [
                        new() {LightAction = LightAction.TurnOn, Light = "light.light1"},
                        new() {LightAction = LightAction.TurnOn, Light = "light.light2"},
                        new() {LightAction = LightAction.TurnOn, Light = "light.light3"},
                        new() {LightAction = LightAction.TurnOn, Light = "light.light4"},

                    ],
                },
            ];

            _config.OffActions =
            [
                new() {LightAction = LightAction.TurnOff, Light = "light.light1"},
                new() {LightAction = LightAction.TurnOff, Light = "light.light2"},
                new() {LightAction = LightAction.TurnOff, Light = "light.light3"}
            ];

            return this;
        }

        public RoomBuilder WithOffActionInFlexiScene()
        {
            _config.FlexiScenes = new List<FlexiSceneConfig>
            {
                new()
                {
                    Name = "default",
                    Actions = new List<ActionConfig>
                    {
                        new() {LightAction = LightAction.TurnOn, Light = "light.light1"},
                    },
                },
                new()
                {
                    Name = "off",
                    Actions = new List<ActionConfig>
                    {
                        new() {ExecuteOffActions = true},
                    },
                },
            };

            _config.OffActions = new List<ActionConfig>
            {
                new() {LightAction = LightAction.TurnOff, Light = "light.light1"},
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
                        new() {Scene = "scene.SOHO"}
                    },
                },
            };

            _config.OffActions = new List<ActionConfig>
            {
                new() {LightAction = LightAction.TurnOff, Light = "light.light1"},
            };

            return this;
        }

        public RoomBuilder WithScriptActions()
        {
            _config.FlexiScenes = new List<FlexiSceneConfig>
            {
                new()
                {
                    Name = "default",
                    Actions = new List<ActionConfig>
                    {
                        new() {Script = "script.wake_up_pc", ScriptData = new Dictionary<string, string> { { "macAddress", "AB:CD:EF:12:34:56" } }}
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
                    Conditions = new List<ConditionConfig> {new() {Binary = "binary_sensor.operating_mode_morning"}},
                    Actions = new List<ActionConfig> {new() {LightAction = LightAction.TurnOn, Light = "light.morning"}},
                    TurnOffAfterIfTriggeredByMotionSensor = TimeSpan.FromMinutes(15),
                    TurnOffAfterIfTriggeredBySwitch = TimeSpan.FromHours(2)
                },
                new()
                {
                    Name = "day",
                    Conditions = new List<ConditionConfig> {new() {Binary = "binary_sensor.operating_mode_day"}},
                    Actions = new List<ActionConfig> {new() {LightAction = LightAction.TurnOn, Light = "light.day" } },
                    TurnOffAfterIfTriggeredByMotionSensor = TimeSpan.FromMinutes(5),
                    TurnOffAfterIfTriggeredBySwitch = TimeSpan.FromHours(4)
                },
                new()
                {
                    Name = "evening",
                    Conditions = new List<ConditionConfig> {new() {Binary = "binary_sensor.operating_mode_evening"}},
                    Actions = new List<ActionConfig> {new() {LightAction = LightAction.TurnOn, Light = "light.evening" } },
                    TurnOffAfterIfTriggeredByMotionSensor = TimeSpan.FromMinutes(60),
                    TurnOffAfterIfTriggeredBySwitch = TimeSpan.FromHours(1)
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

        public RoomBuilder WithNegativeCondition()
        {
            _config.FlexiScenes = new List<FlexiSceneConfig>
            {
                new()
                {
                    Name = "day",
                    Conditions = new List<ConditionConfig> {new() {Binary = "!binary_sensor.operating_mode_morning" } },
                    Actions = new List<ActionConfig> {new() {LightAction = LightAction.TurnOn, Light = "light.day" } },
                    TurnOffAfterIfTriggeredByMotionSensor = TimeSpan.FromMinutes(5),
                    TurnOffAfterIfTriggeredBySwitch = TimeSpan.FromHours(4)
                },
                new()
                {
                    Name = "morning",
                    Actions = new List<ActionConfig> {new() {LightAction = LightAction.TurnOn, Light = "light.morning"}},
                    TurnOffAfterIfTriggeredByMotionSensor = TimeSpan.FromMinutes(15),
                    TurnOffAfterIfTriggeredBySwitch = TimeSpan.FromHours(2)
                },
            };

            _config.OffActions = new List<ActionConfig>
            {
                new() {LightAction = LightAction.TurnOff, Light = "light.morning"},
                new() {LightAction = LightAction.TurnOff, Light = "light.day"}
            };

            return this;
        }

        public RoomBuilder WithAllKindsOfConditions()
        {
            _config.FlexiScenes = new List<FlexiSceneConfig>
            {
                new()
                {
                    Name = "night",
                    Conditions = new List<ConditionConfig>
                    {
                        new() { And =
                            new List<ConditionConfig> {
                                new() { Or =
                                    new List<ConditionConfig> {
                                        new () {Binary = "binary_sensor.operating_mode_night" },
                                        new () {Binary = "binary_sensor.operating_mode_evening" }
                                    }
                                },
                                new() { Binary = "!binary_sensor.operating_mode_peak_hour_morning"}
                            }
                        }
                    },
                    Actions = new List<ActionConfig> {new() {LightAction = LightAction.TurnOn, Light = "light.night" } },
                    TurnOffAfterIfTriggeredByMotionSensor = TimeSpan.FromMinutes(5),
                    TurnOffAfterIfTriggeredBySwitch = TimeSpan.FromHours(4)
                },
                new()
                {
                    Name = "default",
                    Actions = new List<ActionConfig> {new() {LightAction = LightAction.TurnOn, Light = "light.day"}},
                    TurnOffAfterIfTriggeredByMotionSensor = TimeSpan.FromMinutes(15),
                    TurnOffAfterIfTriggeredBySwitch = TimeSpan.FromHours(2)
                },
            };

            _config.OffActions = new List<ActionConfig>
            {
                new() {LightAction = LightAction.TurnOff, Light = "light.night"},
                new() {LightAction = LightAction.TurnOff, Light = "light.day"}
            };

            return this;
        }

        public RoomBuilder WithMultipleFlexiScenesLimitedNext()
        {
            _config.FlexiScenes = new List<FlexiSceneConfig>
            {
                new()
                {
                    Name = "morning",
                    Conditions = new List<ConditionConfig> {new() {Binary = "binary_sensor.operating_mode_morning"}},
                    Actions = new List<ActionConfig> {new() {LightAction = LightAction.TurnOn, Light = "light.morning"}},
                    TurnOffAfterIfTriggeredByMotionSensor = TimeSpan.FromMinutes(15),
                    TurnOffAfterIfTriggeredBySwitch = TimeSpan.FromHours(2),
                },
                new()
                {
                    Name = "day",
                    Conditions = new List<ConditionConfig> {new() {Binary = "binary_sensor.operating_mode_day"}},
                    Actions = new List<ActionConfig> {new() {LightAction = LightAction.TurnOn, Light = "light.day" } },
                    TurnOffAfterIfTriggeredByMotionSensor = TimeSpan.FromMinutes(5),
                    TurnOffAfterIfTriggeredBySwitch = TimeSpan.FromHours(4),
                    NextFlexiScenes = new List<string> { "morning" }
                },
                new()
                {
                    Name = "evening",
                    Conditions = new List<ConditionConfig> {new() {Binary = "binary_sensor.operating_mode_evening"}},
                    Actions = new List<ActionConfig> {new() {LightAction = LightAction.TurnOn, Light = "light.evening" } },
                    TurnOffAfterIfTriggeredByMotionSensor = TimeSpan.FromMinutes(60),
                    TurnOffAfterIfTriggeredBySwitch = TimeSpan.FromHours(1)
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

        public RoomBuilder WithComplexConditions()
        {
            _config.FlexiScenes = new List<FlexiSceneConfig>
            {
                new()
                {
                    Name = "advent - on vacation",
                    Conditions = new List<ConditionConfig> {new () {
                        And =  new List<ConditionConfig>
                    {
                        new() { Or = new List<ConditionConfig>()
                        {
                            new() {Binary = "binary_sensor.operating_mode_morning"},
                            new() {Binary = "binary_sensor.operating_mode_evening"}
                        } },
                        new() { Binary = "binary_sensor.operating_mode_advent" },
                        new() { Binary = "binary_sensor.operating_mode_vacation" }
                    } } },
                    Actions = new List<ActionConfig>
                    {

                        new() {Scene = "scene.kevin_mccallister"}
                    },
                },
                new()
                {
                    Name = "morning - advent",
                    Conditions = new List<ConditionConfig> {new () { And =  new List<ConditionConfig> {new() {Binary = "binary_sensor.operating_mode_morning"}, new() { Binary = "binary_sensor.operating_mode_advent" }} } },
                    Actions = new List<ActionConfig>
                    {

                        new() {Scene = "scene.morning"},
                        new() {LightAction = LightAction.TurnOn, Light = "light.christmas_tree"}
                    },
                },
                new()
                {
                    Name = "morning",
                    Conditions = new List<ConditionConfig> {new() {Binary = "binary_sensor.operating_mode_morning"}},
                    Actions = new List<ActionConfig> {new() { Scene = "scene.morning" }},
                },
                new()
                {
                    Name = "day",
                    Conditions = new List<ConditionConfig> {new() {Binary = "binary_sensor.operating_mode_day"}},
                    Actions = new List<ActionConfig> {new() {LightAction = LightAction.TurnOn, Light = "light.day" } },
                },
                new()
                {
                    Name = "evening - Party",
                    Conditions = new List<ConditionConfig> {new () { And = [new() {Binary = "binary_sensor.operating_mode_evening"}, new() {Binary = "binary_sensor.operating_mode_party"}]} },
                    Actions = new List<ActionConfig> {new() {Scene = "scene.party" } },
                },
                new()
                {
                    Name = "evening - TV",
                    Conditions = new List<ConditionConfig> {new () { And = [new() {Binary = "binary_sensor.operating_mode_evening"}, new() {Binary = "binary_sensor.watching_tv"}]} },
                    Actions = new List<ActionConfig> {new() {Scene = "scene.tv" } },
                },
                new()
                {
                    Name = "evening",
                    Conditions = new List<ConditionConfig> {new() {Binary = "binary_sensor.operating_mode_evening"}},
                    Actions = new List<ActionConfig> {new() { Scene = "scene.evening" } },
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
        public RoomBuilder WithAutoTransitionTurnOfIfNoValidSceneFound()
        {
            _config.AutoTransitionTurnOffIfNoValidSceneFound = true;
            return this;
        }
        public RoomBuilder WithIlluminanceSensor()
        {
            _config.AutoSwitchOffAboveIlluminance = true;
            _config.IlluminanceThreshold = 40;
            _config.IlluminanceLowerThreshold = 20;
            _config.IlluminanceSensors = new List<string>
            {
                "sensor.illuminance"
            };

            return this;
        }

        public RoomBuilder WithIlluminanceSensors()
        {
            _config.AutoSwitchOffAboveIlluminance = true;
            _config.IlluminanceThreshold = 40;
            _config.IlluminanceLowerThreshold = 20;
            _config.IlluminanceSensors = new List<string>
            {
                "sensor.illuminance1",
                "sensor.illuminance2",
            };

            return this;
        }

        public RoomBuilder WithSwitch()
        {
            _config.Switches = new List<SwitchConfig>
            {
                new() { State = "sensor.switch" }
            };

            _config.ClickInterval = TimeSpan.FromMilliseconds(10);
            _config.DoubleClickActions = new List<ActionConfig>
            {
                new() {Light = "light.day", LightAction = LightAction.TurnOff},
            };

            _config.TripleClickActions = new List<ActionConfig>
            {
                new() {Light = "light.evening", LightAction = LightAction.TurnOff},
            };

            _config.LongClickDuration = TimeSpan.FromMilliseconds(20);
            _config.LongClickActions = new List<ActionConfig>
            {
                new() {Light = "light.morning", LightAction = LightAction.TurnOff},
            };
            return this;
        }


        public RoomBuilder WithExecuteOffActionsSwitch()
        {
            _config.Switches = new List<SwitchConfig>
            {
                new() { State = "sensor.switch" }
            };

            _config.ClickInterval = TimeSpan.FromMilliseconds(10);

            _config.TripleClickActions = new List<ActionConfig>
            {
                new() {Light = "light.evening", LightAction = LightAction.TurnOff},
            };
            return this;
        }

        public RoomBuilder WithSimulatePresenceSensor()
        {
            _config.SimulatePresenceSensor = "input_boolean.away";
            return this;
        }


        public RoomBuilder FullyAutomated()
        {
            _config.AutoTransition = true;
            _config.MotionSensors = new List<MotionSensorConfig>();

            return this;
        }


        public RoomBuilder WithUberLongClickActions()
        {
            _config.UberLongClickDuration = TimeSpan.FromMilliseconds(50);
            _config.UberLongClickActions = new List<ActionConfig>
            {
                new() {ExecuteOffActions = true},
            };

            return this;
        }

        public RoomBuilder WithOffSensor()
        {
            _config.OffSensors = new List<string>
            {
                "binary_sensor.triple_click"
            };

            return this;
        }


        public RoomBuilder WithSwitchChangeOFfDurationAndGoToNextAutomationAtInitialOnClickAfterMotion()
        {
            _config.InitialClickAfterMotionBehaviour = InitialClickAfterMotionBehaviour.ChangeOFfDurationAndGoToNextFlexiScene;


            return this;
        }



        public Room Build()
        {
            return new Room(_testCtx.HaContext, _logger, _testCtx.Scheduler, _mqttEntityManager, _fileStorage, _config, TimeSpan.Zero);
        }
    }

}
