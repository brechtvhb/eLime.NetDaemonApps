# Smart Ventilation

The Smart Ventilation app intelligently manages HVAC/ventilation systems based on multiple environmental factors. It monitors indoor air quality, humidity, temperature, and occupancy to automatically adjust ventilation levels for optimal comfort, health, and energy efficiency.

## Features

- **Indoor Air Quality Management**: Monitors CO2 levels and adjusts ventilation accordingly
- **Bathroom Humidity Control**: Increases ventilation during high humidity periods
- **Mold Prevention**: Ensures minimum ventilation runtime to prevent mold growth
- **Indoor Temperature Management**: Optimizes ventilation based on indoor/outdoor temperature differentials
- **Dry Air Prevention**: Reduces ventilation when indoor air becomes too dry
- **Energy Saving**: Reduces ventilation when away or sleeping to save electricity
- **State Ping-Pong Prevention**: Prevents rapid toggling between ventilation modes
- **Multi-Guard System**: Multiple configurable "guards" that work together to determine optimal ventilation level

## Configuration

The Smart Ventilation app is configured using the `SmartVentilationConfig` class in your `appsettings.json`.

### Basic Configuration Structure

```json
{
  "SmartVentilation": {
    "Name": "Home Ventilation",
    "Enabled": true,
    "NetDaemonUserId": "netdaemon_user_id",
    "ClimateEntity": "climate.ventilation_system",
    "StatePingPong": {
      "TimeoutSpan": "00:15:00"
    },
    "Indoor": {
      "Co2Sensors": ["sensor.living_room_co2", "sensor.bedroom_co2"],
      "Co2MediumThreshold": 800,
      "Co2HighThreshold": 1200
    },
    "Bathroom": {
      "HumiditySensors": ["sensor.bathroom_humidity"],
      "HumidityMediumThreshold": 70,
      "HumidityHighThreshold": 80
    },
    "Mold": {
      "MaxAwayTimeSpan": "08:00:00",
      "RechargeTimeSpan": "01:00:00"
    },
    "IndoorTemperature": {
      "SummerModeSensor": "binary_sensor.summer_mode",
      "OutdoorTemperatureSensor": "sensor.outdoor_temperature",
      "PostHeatExchangerTemperatureSensor": "sensor.ventilation_supply_temperature"
    },
    "DryAir": {
      "HumiditySensors": ["sensor.living_room_humidity", "sensor.bedroom_humidity"],
      "HumidityLowThreshold": 35,
      "OutdoorTemperatureSensor": "sensor.outdoor_temperature",
      "MaxOutdoorTemperature": 10
    },
    "ElectricityBill": {
      "AwaySensor": "binary_sensor.family_away",
      "SleepingSensor": "binary_sensor.family_sleeping"
    }
  }
}
```

### Main Configuration Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Name` | string | Yes | Friendly name for the ventilation system |
| `Enabled` | bool | No | Enable/disable the automation (default: true) |
| `NetDaemonUserId` | string | Yes | User ID for NetDaemon (distinguishes automated vs manual actions) |
| `ClimateEntity` | string | Yes | Climate entity controlling the ventilation system |
| `StatePingPong` | object | No | State ping-pong prevention configuration |
| `Indoor` | object | No | Indoor air quality guard configuration |
| `Bathroom` | object | No | Bathroom humidity guard configuration |
| `Mold` | object | No | Mold prevention guard configuration |
| `IndoorTemperature` | object | No | Indoor temperature guard configuration |
| `DryAir` | object | No | Dry air prevention guard configuration |
| `ElectricityBill` | object | No | Energy saving guard configuration |

### State Ping-Pong Guard

Prevents the system from rapidly switching between ventilation levels.

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `TimeoutSpan` | TimeSpan | Yes | Minimum time between ventilation level changes |

**Example**:
```json
"StatePingPong": {
  "TimeoutSpan": "00:15:00"
}
```

This ensures the system waits at least 15 minutes after changing ventilation level before changing it again.

### Indoor Air Quality Guard

Monitors CO2 levels to maintain healthy indoor air quality.

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Co2Sensors` | string[] | Yes | List of CO2 sensor entities (ppm) |
| `Co2MediumThreshold` | int | Yes | CO2 level to trigger medium ventilation (ppm) |
| `Co2HighThreshold` | int | Yes | CO2 level to trigger high ventilation (ppm) |

**Example**:
```json
"Indoor": {
  "Co2Sensors": [
    "sensor.living_room_co2",
    "sensor.bedroom_co2",
    "sensor.office_co2"
  ],
  "Co2MediumThreshold": 800,
  "Co2HighThreshold": 1200
}
```

**Logic**:
- CO2 < MediumThreshold: Low ventilation sufficient
- CO2 ? MediumThreshold: Increase to medium ventilation
- CO2 ? HighThreshold: Increase to high ventilation

### Bathroom Humidity Guard

Manages ventilation during bathroom use (showers, baths).

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `HumiditySensors` | string[] | Yes | List of bathroom humidity sensor entities (%) |
| `HumidityMediumThreshold` | int | Yes | Humidity level to trigger medium ventilation (%) |
| `HumidityHighThreshold` | int | Yes | Humidity level to trigger high ventilation (%) |

**Example**:
```json
"Bathroom": {
  "HumiditySensors": [
    "sensor.bathroom_1_humidity",
    "sensor.bathroom_2_humidity"
  ],
  "HumidityMediumThreshold": 70,
  "HumidityHighThreshold": 80
}
```

**Logic**:
- Humidity < MediumThreshold: Normal ventilation
- Humidity ? MediumThreshold: Increase to medium ventilation
- Humidity ? HighThreshold: Increase to high ventilation

### Mold Prevention Guard

Ensures minimum ventilation runtime to prevent mold growth, especially when away from home.

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `MaxAwayTimeSpan` | TimeSpan | Yes | Maximum time ventilation can be in low/away mode |
| `RechargeTimeSpan` | TimeSpan | Yes | Duration to run higher ventilation after timeout |

**Example**:
```json
"Mold": {
  "MaxAwayTimeSpan": "08:00:00",
  "RechargeTimeSpan": "01:00:00"
}
```

**Logic**:
- If ventilation has been in low/away mode for 8 hours, force higher ventilation for 1 hour
- Prevents stale air and mold growth during extended periods of low ventilation
- Particularly useful when away from home for extended periods

### Indoor Temperature Guard

Optimizes ventilation based on indoor and outdoor temperature differentials, especially useful for heat recovery ventilation systems.

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `SummerModeSensor` | string | No | Binary sensor indicating summer mode |
| `OutdoorTemperatureSensor` | string | No | Outdoor temperature sensor |
| `PostHeatExchangerTemperatureSensor` | string | No | Supply air temperature sensor (after heat exchanger) |

**Example**:
```json
"IndoorTemperature": {
  "SummerModeSensor": "binary_sensor.summer_mode",
  "OutdoorTemperatureSensor": "sensor.outdoor_temp",
  "PostHeatExchangerTemperatureSensor": "sensor.supply_air_temp"
}
```

**Logic**:
- In summer: Reduces ventilation if bringing in warm outdoor air would increase cooling load
- In winter: Optimizes ventilation based on heat recovery efficiency
- Monitors supply air temperature to ensure heat exchanger is working effectively

### Dry Air Prevention Guard

Prevents indoor air from becoming too dry, which can cause health and comfort issues.

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `HumiditySensors` | string[] | Yes | List of indoor humidity sensor entities (%) |
| `HumidityLowThreshold` | int | Yes | Humidity level below which to reduce ventilation (%) |
| `OutdoorTemperatureSensor` | string | Yes | Outdoor temperature sensor |
| `MaxOutdoorTemperature` | int | Yes | Maximum outdoor temperature for dry air concern (°C) |

**Example**:
```json
"DryAir": {
  "HumiditySensors": [
    "sensor.living_room_humidity",
    "sensor.bedroom_humidity"
  ],
  "HumidityLowThreshold": 35,
  "OutdoorTemperatureSensor": "sensor.outdoor_temp",
  "MaxOutdoorTemperature": 10
}
```

**Logic**:
- When outdoor temperature < MaxOutdoorTemperature (cold air holds less moisture)
- AND indoor humidity < HumidityLowThreshold
- THEN reduce ventilation to prevent further drying
- Particularly important in winter when cold outdoor air is very dry

### Electricity Bill Guard

Reduces ventilation during away and sleeping periods to save energy while maintaining air quality.

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `AwaySensor` | string | No | Binary sensor indicating no one is home |
| `SleepingSensor` | string | No | Binary sensor indicating family is sleeping |

**Example**:
```json
"ElectricityBill": {
  "AwaySensor": "binary_sensor.family_away",
  "SleepingSensor": "binary_sensor.family_sleeping"
}
```

**Logic**:
- When away: Reduce to minimum ventilation (unless other guards require higher)
- When sleeping: Reduce ventilation slightly (unless CO2 levels require higher)
- Saves energy while maintaining minimum air quality standards
- Mold guard prevents excessive low ventilation periods

## Complete Example Configuration

```json
{
  "SmartVentilation": {
    "Name": "Home Ventilation System",
    "Enabled": true,
    "NetDaemonUserId": "12345678-1234-1234-1234-123456789012",
    "ClimateEntity": "climate.zehnder_comfoair",
    "StatePingPong": {
      "TimeoutSpan": "00:15:00"
    },
    "Indoor": {
      "Co2Sensors": [
        "sensor.living_room_co2",
        "sensor.bedroom_co2",
        "sensor.kids_room_co2",
        "sensor.office_co2"
      ],
      "Co2MediumThreshold": 800,
      "Co2HighThreshold": 1200
    },
    "Bathroom": {
      "HumiditySensors": [
        "sensor.main_bathroom_humidity",
        "sensor.ensuite_humidity"
      ],
      "HumidityMediumThreshold": 70,
      "HumidityHighThreshold": 80
    },
    "Mold": {
      "MaxAwayTimeSpan": "08:00:00",
      "RechargeTimeSpan": "01:00:00"
    },
    "IndoorTemperature": {
      "SummerModeSensor": "binary_sensor.summer_mode",
      "OutdoorTemperatureSensor": "sensor.outdoor_temperature",
      "PostHeatExchangerTemperatureSensor": "sensor.supply_air_temperature"
    },
    "DryAir": {
      "HumiditySensors": [
        "sensor.living_room_humidity",
        "sensor.bedroom_humidity",
        "sensor.kids_room_humidity"
      ],
      "HumidityLowThreshold": 35,
      "OutdoorTemperatureSensor": "sensor.outdoor_temperature",
      "MaxOutdoorTemperature": 10
    },
    "ElectricityBill": {
      "AwaySensor": "binary_sensor.family_away",
      "SleepingSensor": "binary_sensor.family_sleeping"
    }
  }
}
```

## How It Works

The Smart Ventilation system uses a multi-guard approach where each guard "votes" for a ventilation level:

1. **Collect Votes**: Each enabled guard evaluates current conditions and suggests a ventilation level
2. **Determine Highest Need**: The system selects the highest ventilation level requested by any guard
3. **Apply Ping-Pong Prevention**: Respects minimum timeout between changes
4. **Execute**: Sets the ventilation system to the determined level

### Ventilation Levels

Typical levels (depends on your climate entity):
- **Off/Away**: Minimum ventilation (when away for short periods)
- **Low**: Basic ventilation (normal when home, good air quality)
- **Medium**: Enhanced ventilation (elevated CO2, moderate humidity)
- **High**: Maximum ventilation (high CO2, high humidity, rapid air exchange needed)

### Guard Priority

While all guards vote, practical priorities emerge:
1. **Mold Prevention**: Overrides away mode after timeout
2. **Indoor Air Quality (CO2)**: High priority for health
3. **Bathroom Humidity**: High priority when active
4. **Dry Air Prevention**: Prevents going higher
5. **Temperature**: Optimizes efficiency
6. **Electricity Bill**: Suggests lower levels when possible

## Usage Tips

- **Sensor Placement**: CO2 sensors in living areas and bedrooms, humidity sensors in bathrooms and main living spaces
- **Thresholds**: Adjust based on your home's characteristics and personal preferences
  - CO2: 800-1200 ppm is typical; lower for better air quality, higher to save energy
  - Bathroom humidity: 70-80% typical; adjust based on fan effectiveness
  - Dry air: 35-45% typical; lower in very dry climates
- **Away Mode**: Use a presence detection system to reliably detect when home is empty
- **Sleep Mode**: Can be based on time, bedroom occupancy, or both
- **Mold Prevention**: 8-hour away span works well for typical work days; adjust for longer trips
- **Climate Entity**: Ensure your ventilation system integrates with Home Assistant (ZeroConf, MQTT, or custom integration)

## MQTT Sensors

The app may create MQTT sensors for monitoring:
- Current ventilation demand level
- Active guards and their votes
- Time since last ventilation change
- Mold prevention countdown

## Integration with Other Apps

- **Energy Manager**: Can be configured as a consumer to optimize ventilation during solar production
- **FlexiScreens**: Coordinates with window management for free cooling/heating
- **Smart Heat Pump**: Works together for optimal climate control

## Troubleshooting

**Ventilation changes too frequently**:
- Increase `StatePingPong.TimeoutSpan`
- Adjust thresholds to have more hysteresis

**Air quality issues (high CO2)**:
- Lower `Co2MediumThreshold` and/or `Co2HighThreshold`
- Check if ElectricityBill guard is too aggressive

**Too dry in winter**:
- Increase `DryAir.HumidityLowThreshold`
- Lower `DryAir.MaxOutdoorTemperature`

**Mold prevention activating too often**:
- Increase `Mold.MaxAwayTimeSpan`
- Decrease `Mold.RechargeTimeSpan`

**High energy bills**:
- Enable all guards to ensure away/sleep modes activate
- Verify presence detection is working correctly
- Consider lowering thresholds slightly
