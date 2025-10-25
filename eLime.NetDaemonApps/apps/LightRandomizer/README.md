# Lights Randomizer

The Lights Randomizer app creates realistic lighting patterns to simulate presence when you're away from home. It randomly activates different lighting zones at realistic times and durations to make your home appear occupied, enhancing security.

## Features

- **Random Zone Selection**: Randomly selects which zones to light up
- **Configurable Zone Count**: Control how many zones are active simultaneously
- **Scene-Based Activation**: Uses predefined scenes for realistic lighting
- **Guard Control**: Enable/disable via a binary sensor (e.g., away mode)
- **Time-Based Randomization**: Varies timing to appear natural
- **Multiple Zones**: Support for different areas of your home

## Configuration

The Lights Randomizer is configured using the `LightsRandomizerConfig` class in your `appsettings.json`.

### Configuration Structure

```json
{
  "LightsRandomizer": {
    "LightingAllowedSensor": "binary_sensor.family_away",
    "AmountOfZonesToLight": 2,
    "Zones": [
      {
        "Name": "Living Room",
        "Scenes": [
          "scene.living_room_evening",
          "scene.living_room_tv",
          "scene.living_room_reading"
        ]
      },
      {
        "Name": "Kitchen",
        "Scenes": [
          "scene.kitchen_bright",
          "scene.kitchen_evening"
        ]
      },
      {
        "Name": "Bedroom",
        "Scenes": [
          "scene.bedroom_evening",
          "scene.bedroom_dim"
        ]
      },
      {
        "Name": "Office",
        "Scenes": [
          "scene.office_work"
        ]
      }
    ]
  }
}
```

### Configuration Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `LightingAllowedSensor` | string | Yes | Binary sensor controlling when randomization is active (e.g., away mode sensor) |
| `AmountOfZonesToLight` | int | Yes | Number of zones to have lit simultaneously |
| `Zones` | LightingZoneConfig[] | Yes | Array of lighting zone configurations |

### Lighting Zone Configuration

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Name` | string | Yes | Friendly name for the lighting zone |
| `Scenes` | string[] | Yes | List of Home Assistant scene entity IDs that can be activated for this zone |

## Complete Example Configuration

```json
{
  "LightsRandomizer": {
    "LightingAllowedSensor": "binary_sensor.vacation_mode",
    "AmountOfZonesToLight": 3,
    "Zones": [
      {
        "Name": "Living Room",
        "Scenes": [
          "scene.living_room_evening_relax",
          "scene.living_room_tv_watching",
          "scene.living_room_reading",
          "scene.living_room_dinner"
        ]
      },
      {
        "Name": "Kitchen",
        "Scenes": [
          "scene.kitchen_cooking",
          "scene.kitchen_bright",
          "scene.kitchen_ambient"
        ]
      },
      {
        "Name": "Dining Room",
        "Scenes": [
          "scene.dining_room_dinner",
          "scene.dining_room_ambient"
        ]
      },
      {
        "Name": "Master Bedroom",
        "Scenes": [
          "scene.bedroom_evening",
          "scene.bedroom_reading",
          "scene.bedroom_dim"
        ]
      },
      {
        "Name": "Office",
        "Scenes": [
          "scene.office_work",
          "scene.office_evening"
        ]
      },
      {
        "Name": "Hallway",
        "Scenes": [
          "scene.hallway_on"
        ]
      },
      {
        "Name": "Bathroom",
        "Scenes": [
          "scene.bathroom_bright",
          "scene.bathroom_evening"
        ]
      }
    ]
  }
}
```

## How It Works

1. **Monitoring**: Continuously monitors the `LightingAllowedSensor`
2. **Activation**: When sensor turns ON (e.g., away mode activated):
   - Randomly selects `AmountOfZonesToLight` zones from the available zones
   - For each selected zone, randomly picks one of its scenes
   - Activates the selected scenes
3. **Randomization**: Periodically changes which zones are active and which scenes are used
4. **Timing**: Varies activation times and durations to appear natural
5. **Deactivation**: When sensor turns OFF (e.g., returning home), stops the randomization

### Selection Algorithm

- **Zone Selection**: Randomly selects N zones without replacement (each zone lit only once)
- **Scene Selection**: Randomly picks one scene from each selected zone's scene list
- **Duration**: Random duration for how long each scene stays active
- **Interval**: Random intervals between scene changes

## Usage Tips

### Sensor Configuration

Create a binary sensor for controlling the randomizer. Examples:

**Simple away mode**:
```yaml
binary_sensor:
  - platform: template
    sensors:
      family_away:
        friendly_name: "Family Away"
        value_template: >
          {{ states('person.person1') == 'not_home' and 
             states('person.person2') == 'not_home' }}
```

**Away with time restriction**:
```yaml
binary_sensor:
  - platform: template
    sensors:
      lighting_simulation_active:
        friendly_name: "Lighting Simulation Active"
        value_template: >
          {{ states('input_boolean.vacation_mode') == 'on' and
             now().hour >= 17 and now().hour <= 23 }}
```

### Zone Design

- **Variety**: Include 5-7 zones for realistic variety
- **Common Areas**: Focus on living room, kitchen, and areas visible from outside
- **Scene Diversity**: Provide 2-4 scenes per zone for variation
- **Realistic Scenes**: Use scenes that would naturally be active (evening/night lighting)
- **Exterior Visibility**: Prioritize zones visible from the street

### Amount of Zones

- **Small apartment**: 1-2 zones
- **Medium house**: 2-3 zones
- **Large house**: 3-4 zones
- **Very large house**: 4-5 zones

More zones active simultaneously = higher electricity cost but more realistic.

### Scene Configuration

Create realistic scenes in Home Assistant:

```yaml
scene:
  - name: Living Room Evening
    entities:
      light.living_room_ceiling:
        state: on
        brightness: 180
        kelvin: 3000
      light.living_room_lamp:
        state: on
        brightness: 120
        kelvin: 2700
      light.living_room_tv_backlight:
        state: on
        brightness: 40

  - name: Living Room TV Watching
    entities:
      light.living_room_ceiling:
        state: off
      light.living_room_lamp:
        state: on
        brightness: 60
        kelvin: 2200
      light.living_room_tv_backlight:
        state: on
        brightness: 60
```

## Best Practices

1. **Test First**: Test with `AmountOfZonesToLight: 1` to verify zone selection works correctly
2. **Realistic Timing**: Only run during evening hours (17:00-23:00) for maximum realism
3. **Exterior Zones**: Include at least one zone visible from outside
4. **Common Patterns**: Match your actual usage patterns (e.g., if you always have kitchen light on in evening, include it more often)
5. **Energy Cost**: Balance realism with electricity costs
6. **Scene Preparation**: Pre-create all scenes in Home Assistant before configuring
7. **Manual Override**: Consider adding a way to quickly disable if you return home early

## Integration with Other Apps

- **Energy Manager**: Can be configured to prefer solar hours if you have battery backup
- **FlexiLights**: Disable FlexiLights automation in affected rooms while randomizer is active
- **Smart Ventilation**: Consider reduced ventilation during away mode to save energy

## Advanced Configuration

### Time-Based Scenes

Create scenes that match typical activities at certain times:

- **17:00-19:00**: Kitchen + Living Room (cooking/dinner time)
- **19:00-22:00**: Living Room + TV area (evening relaxation)
- **22:00-23:00**: Bedroom + Bathroom (bedtime routine)

### Vacation Mode Integration

Combine with a vacation mode input:

```yaml
input_boolean:
  vacation_mode:
    name: Vacation Mode
    icon: mdi:beach

automation:
  - alias: "Vacation Mode Lighting Active"
    trigger:
      - platform: time
        at: "17:00:00"
    condition:
      - condition: state
        entity_id: input_boolean.vacation_mode
        state: 'on'
    action:
      - service: input_boolean.turn_on
        target:
          entity_id: binary_sensor.lighting_simulation_active
```

## Troubleshooting

**Lights don't turn on**:
- Verify `LightingAllowedSensor` is in "on" state
- Check that scene entity IDs are correct
- Ensure scenes are properly configured in Home Assistant

**Same zone always selected**:
- Increase the number of zones in configuration
- Verify zone names are unique

**Not realistic enough**:
- Add more scenes per zone
- Adjust timing parameters
- Include more zones in configuration

**Too many lights on (high energy cost)**:
- Reduce `AmountOfZonesToLight`
- Use dimmer scenes
- Limit operating hours

## Security Considerations

- **Combine with other measures**: Don't rely solely on lighting for security
- **Vary the pattern**: With multiple scenes per zone, patterns are less predictable
- **Consider smart curtains**: Close curtains to prevent visibility of empty rooms
- **Door/window sensors**: Integrate with alarm system
- **Notifications**: Get alerts if doors opened while randomizer is active
