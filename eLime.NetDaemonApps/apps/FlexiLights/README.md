# FlexiLights

The FlexiLights app provides advanced, context-aware lighting automation for your home. It goes beyond simple on/off automation by supporting multiple scenes per room, complex conditions, motion sensors, switches with multiple click patterns, illuminance-based control, and intelligent scene transitions.

## Features

- **FlexiScenes**: Define multiple lighting scenes per room with complex activation conditions
- **Context-Aware Automation**: Scenes activate based on time, occupancy, illuminance, and custom sensors
- **Motion Sensor Integration**: Automatic activation with configurable timeouts
- **Advanced Switch Control**: Single, double, triple, long, and ultra-long press actions
- **Illuminance-Based Control**: Only activate lights when needed based on light levels
- **Scene Chaining**: Automatically transition through scenes or cycle with button presses
- **Presence Simulation**: Random lighting patterns when away
- **Per-Room Configuration**: Individual settings for each room/zone
- **Manual Override Support**: Respects manual adjustments with configurable ignore periods

## Configuration

The FlexiLights app is configured using the `FlexLightConfig` class in your `appsettings.json`.

### Basic Configuration Structure

```json
{
  "FlexiLights": {
    "Rooms": {
      "living_room": {
        "Name": "Living Room",
        "Enabled": true,
        "AutoTransition": true,
        "AutoTransitionTurnOffIfNoValidSceneFound": true,
        "IlluminanceSensors": ["sensor.living_room_illuminance"],
        "IlluminanceThreshold": 50,
        "IlluminanceLowerThreshold": 30,
        "IlluminanceThresholdTimeSpan": "00:02:00",
        "AutoSwitchOffAboveIlluminance": true,
        "MotionSensors": [
          {
            "Sensor": "binary_sensor.living_room_motion",
            "MixinScene": "living_room_motion"
          }
        ],
        "Switches": [
          {
            "Binary": "binary_sensor.living_room_switch",
            "State": "on"
          }
        ],
        "ClickInterval": "00:00:00.500",
        "LongClickDuration": "00:00:01",
        "UberLongClickDuration": "00:00:03",
        "InitialClickAfterMotionBehaviour": "ChangeOFfDurationAndGoToNextFlexiScene",
        "IgnorePresenceAfterOffDuration": "00:05:00",
        "FlexiScenes": [
          // See FlexiScene examples below
        ],
        "OffActions": [
          {
            "Light": "light.living_room_all",
            "LightAction": "TurnOff"
          }
        ]
      }
    }
  }
}
```

### Room Configuration Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Name` | string | Yes | Friendly name for the room |
| `Enabled` | bool | No | Enable/disable automation for this room (default: true) |
| `AutoTransition` | bool | No | Automatically transition to matching scenes when conditions change |
| `AutoTransitionTurnOffIfNoValidSceneFound` | bool | No | Turn off lights if no scene matches conditions |
| `SimulatePresenceSensor` | string | No | Binary sensor triggering presence simulation mode |
| `SimulatePresenceIgnoreDuration` | TimeSpan | No | How long to ignore automation after presence simulation |
| `IlluminanceSensors` | string[] | No | List of illuminance sensors for the room |
| `IlluminanceThreshold` | int | No | Lux level above which lights won't turn on |
| `IlluminanceLowerThreshold` | int | No | Lux level below which lights can turn on (hysteresis) |
| `IlluminanceThresholdTimeSpan` | TimeSpan | No | Time illuminance must be below threshold before activating |
| `AutoSwitchOffAboveIlluminance` | bool | No | Automatically turn off when illuminance exceeds threshold |
| `MotionSensors` | MotionSensorConfig[] | No | Motion sensor configurations |
| `IgnorePresenceAfterOffDuration` | TimeSpan | No | Ignore motion after manual off for this duration |
| `Switches` | SwitchConfig[] | No | Physical switch configurations |
| `ClickInterval` | TimeSpan | No | Maximum time between clicks for multi-click detection |
| `LongClickDuration` | TimeSpan | No | Duration for long press detection |
| `UberLongClickDuration` | TimeSpan | No | Duration for ultra-long press detection |
| `InitialClickAfterMotionBehaviour` | enum | No | Behavior when clicking after motion trigger |
| `OffSensors` | string[] | No | Binary sensors that trigger lights off |
| `FlexiScenes` | FlexiSceneConfig[] | Yes | List of FlexiScene configurations |
| `DoubleClickActions` | ActionConfig[] | No | Actions to execute on double click |
| `TripleClickActions` | ActionConfig[] | No | Actions to execute on triple click |
| `LongClickActions` | ActionConfig[] | No | Actions to execute on long click |
| `UberLongClickActions` | ActionConfig[] | No | Actions to execute on ultra-long click |
| `OffActions` | ActionConfig[] | Yes | Actions to execute when turning off |

### Motion Sensor Configuration

```json
{
  "Sensor": "binary_sensor.living_room_motion",
  "MixinScene": "living_room_bright"
}
```

| Property | Type | Description |
|----------|------|-------------|
| `Sensor` | string | Binary sensor entity for motion detection |
| `MixinScene` | string | FlexiScene to activate (or mix in) when motion detected |

### Switch Configuration

```json
{
  "Binary": "binary_sensor.wall_switch",
  "State": "on"
}
```

| Property | Type | Description |
|----------|------|-------------|
| `Binary` | string | Binary sensor or event entity for the switch |
| `State` | string | State value to match (e.g., "on", "off", "single", "double") |

### FlexiScene Configuration

A FlexiScene defines a specific lighting configuration that can be activated based on conditions.

```json
{
  "Name": "evening_relax",
  "Secondary": false,
  "Conditions": [
    {
      "Binary": "binary_sensor.evening"
    },
    {
      "Binary": "binary_sensor.someone_home"
    }
  ],
  "FullyAutomatedConditions": [
    {
      "Binary": "binary_sensor.auto_mode"
    }
  ],
  "Actions": [
    {
      "Light": "light.living_room_main",
      "LightAction": "TurnOn",
      "Brightness": "40",
      "Color": {
        "Kelvin": 2700
      }
    }
  ],
  "TurnOffAfterIfTriggeredBySwitch": "02:00:00",
  "TurnOffAfterIfTriggeredByMotionSensor": "00:10:00",
  "NextFlexiScenes": ["night_dim", "off"]
}
```

| Property | Type | Description |
|----------|------|-------------|
| `Name` | string | Unique identifier for the scene |
| `Secondary` | bool | If true, scene won't be auto-selected but can be manually triggered |
| `Conditions` | ConditionConfig[] | Conditions that must be met for scene to be valid |
| `FullyAutomatedConditions` | ConditionConfig[] | Additional conditions for fully automated activation |
| `Actions` | ActionConfig[] | Actions to execute when scene activates |
| `TurnOffAfterIfTriggeredBySwitch` | TimeSpan | Auto-off timeout when triggered by switch |
| `TurnOffAfterIfTriggeredByMotionSensor` | TimeSpan | Auto-off timeout when triggered by motion |
| `NextFlexiScenes` | string[] | Scenes to cycle through on subsequent clicks |

### Condition Configuration

Conditions support Boolean logic with AND/OR operators:

**Simple condition**:
```json
{
  "Binary": "binary_sensor.evening"
}
```

**OR conditions**:
```json
{
  "Or": [
    { "Binary": "binary_sensor.movie_mode" },
    { "Binary": "binary_sensor.tv_on" }
  ]
}
```

**AND conditions**:
```json
{
  "And": [
    { "Binary": "binary_sensor.evening" },
    { "Binary": "binary_sensor.someone_home" }
  ]
}
```

**Complex nested conditions**:
```json
{
  "And": [
    { "Binary": "binary_sensor.evening" },
    {
      "Or": [
        { "Binary": "binary_sensor.weekday" },
        { "Binary": "binary_sensor.holiday" }
      ]
    }
  ]
}
```

### Action Configuration

Actions define what happens when a scene activates or button is pressed.

**Light action**:
```json
{
  "Light": "light.living_room_main",
  "LightAction": "TurnOn",
  "Brightness": "80",
  "Color": {
    "Kelvin": 4000
  }
}
```

**Scene action**:
```json
{
  "Scene": "scene.living_room_movie"
}
```

**Switch action**:
```json
{
  "Switch": "switch.tv_outlet",
  "SwitchAction": "TurnOn"
}
```

**Script action**:
```json
{
  "Script": "script.start_movie_night",
  "ScriptData": {
    "room": "living_room"
  }
}
```

**FlexiScene action**:
```json
{
  "FlexiScene": "living_room",
  "FlexiSceneAction": "TurnOn",
  "FlexiSceneToTrigger": "evening_bright"
}
```

#### Light Action Properties

| Property | Type | Description |
|----------|------|-------------|
| `Light` | string | Light entity ID |
| `LightAction` | string | "TurnOn" or "TurnOff" |
| `Brightness` | string | Brightness percentage (0-100) |
| `Color` | Color | Color configuration |
| `Effect` | string | Light effect name |
| `Flash` | string | Flash type ("short" or "long") |

#### Color Configuration

```json
{
  "Kelvin": 4000
}
```

Or:
```json
{
  "Red": 255,
  "Green": 200,
  "Blue": 100
}
```

Or:
```json
{
  "Hue": 120,
  "Saturation": 80
}
```

## Complete Example Configuration

### Living Room with Multiple Scenes

```json
{
  "living_room": {
    "Name": "Living Room",
    "Enabled": true,
    "AutoTransition": true,
    "AutoTransitionTurnOffIfNoValidSceneFound": true,
    "IlluminanceSensors": ["sensor.living_room_lux"],
    "IlluminanceThreshold": 50,
    "IlluminanceLowerThreshold": 30,
    "IlluminanceThresholdTimeSpan": "00:02:00",
    "AutoSwitchOffAboveIlluminance": true,
    "MotionSensors": [
      {
        "Sensor": "binary_sensor.living_room_motion",
        "MixinScene": "living_room_auto"
      }
    ],
    "Switches": [
      {
        "Binary": "binary_sensor.living_room_switch",
        "State": "on"
      }
    ],
    "ClickInterval": "00:00:00.500",
    "LongClickDuration": "00:00:01",
    "UberLongClickDuration": "00:00:03",
    "InitialClickAfterMotionBehaviour": "ChangeOFfDurationAndGoToNextFlexiScene",
    "IgnorePresenceAfterOffDuration": "00:05:00",
    "FlexiScenes": [
      {
        "Name": "morning_bright",
        "Conditions": [
          { "Binary": "binary_sensor.morning" },
          { "Binary": "binary_sensor.someone_home" }
        ],
        "Actions": [
          {
            "Light": "light.living_room_ceiling",
            "LightAction": "TurnOn",
            "Brightness": "100",
            "Color": { "Kelvin": 5000 }
          }
        ],
        "TurnOffAfterIfTriggeredByMotionSensor": "00:15:00",
        "NextFlexiScenes": ["morning_dim"]
      },
      {
        "Name": "morning_dim",
        "Secondary": true,
        "Actions": [
          {
            "Light": "light.living_room_ceiling",
            "LightAction": "TurnOn",
            "Brightness": "40",
            "Color": { "Kelvin": 4000 }
          }
        ],
        "NextFlexiScenes": ["off"]
      },
      {
        "Name": "evening_relax",
        "Conditions": [
          { "Binary": "binary_sensor.evening" },
          { "Binary": "binary_sensor.someone_home" }
        ],
        "Actions": [
          {
            "Light": "light.living_room_floor_lamp",
            "LightAction": "TurnOn",
            "Brightness": "60",
            "Color": { "Kelvin": 2700 }
          },
          {
            "Light": "light.living_room_ceiling",
            "LightAction": "TurnOn",
            "Brightness": "30",
            "Color": { "Kelvin": 2700 }
          }
        ],
        "TurnOffAfterIfTriggeredByMotionSensor": "00:10:00",
        "NextFlexiScenes": ["evening_tv", "evening_bright"]
      },
      {
        "Name": "evening_tv",
        "Secondary": true,
        "Actions": [
          {
            "Light": "light.living_room_tv_backlight",
            "LightAction": "TurnOn",
            "Brightness": "20",
            "Color": { "Kelvin": 2200 }
          }
        ],
        "NextFlexiScenes": ["evening_bright"]
      },
      {
        "Name": "evening_bright",
        "Secondary": true,
        "Actions": [
          {
            "Scene": "scene.living_room_all_on"
          }
        ],
        "NextFlexiScenes": ["off"]
      },
      {
        "Name": "night_dim",
        "Conditions": [
          { "Binary": "binary_sensor.night" },
          { "Binary": "binary_sensor.someone_home" }
        ],
        "Actions": [
          {
            "Light": "light.living_room_floor_lamp",
            "LightAction": "TurnOn",
            "Brightness": "10",
            "Color": { "Kelvin": 2200 }
          }
        ],
        "TurnOffAfterIfTriggeredByMotionSensor": "00:03:00",
        "NextFlexiScenes": ["off"]
      },
      {
        "Name": "living_room_auto",
        "Secondary": true,
        "Actions": [
          {
            "FlexiScene": "living_room",
            "FlexiSceneAction": "TurnOn"
          }
        ],
        "TurnOffAfterIfTriggeredByMotionSensor": "00:05:00"
      }
    ],
    "DoubleClickActions": [
      {
        "Scene": "scene.movie_mode"
      }
    ],
    "LongClickActions": [
      {
        "Light": "light.living_room_all",
        "LightAction": "TurnOff"
      }
    ],
    "OffActions": [
      {
        "Light": "light.living_room_all",
        "LightAction": "TurnOff"
      }
    ]
  }
}
```

## How It Works

### Scene Selection

1. **Evaluate Conditions**: Check all FlexiScenes for matching conditions
2. **Primary Scenes**: Consider non-secondary scenes first
3. **Illuminance Check**: Verify light levels if configured
4. **Best Match**: Select the first matching scene
5. **Execute Actions**: Run the scene's actions

### Motion Detection

When motion is detected:
1. Check if enough time has passed since last manual off
2. Verify illuminance is below threshold
3. Activate the specified MixinScene
4. Set auto-off timer based on scene configuration

### Switch Click Patterns

- **Single Click**: Activate next FlexiScene or best matching scene
- **Double Click**: Execute DoubleClickActions
- **Triple Click**: Execute TripleClickActions  
- **Long Press**: Execute LongClickActions
- **Ultra-Long Press**: Execute UberLongClickActions

### Auto Transition

When `AutoTransition` is enabled:
- System continuously monitors conditions
- When conditions change, automatically switches to matching scene
- If no scene matches and `AutoTransitionTurnOffIfNoValidSceneFound` is true, turns off lights

## Usage Tips

- **Binary Sensors**: Use template binary sensors for complex time/state logic
- **Scene Hierarchy**: Order scenes from most to least specific conditions
- **Secondary Scenes**: Use for manual-only or clickthrough options
- **Illuminance Hysteresis**: Set threshold higher than lower threshold to prevent flickering
- **Motion Timeouts**: Shorter for hallways (3-5 min), longer for living spaces (10-15 min)
- **Click Intervals**: 500ms works well for most users; adjust if multi-clicks are hard to trigger
- **Scene Chaining**: Order NextFlexiScenes from dimmer to off for intuitive control

## Integration with Other Apps

- **FlexiScreens**: Coordinate lighting with window coverings
- **Energy Manager**: Can trigger scenes based on energy availability
- **Smart Ventilation**: Adjust lighting based on room occupancy patterns
