# Smart Irrigation

The Smart Irrigation app provides intelligent water management for gardens, lawns, and plants. It supports multiple irrigation types including soil-based watering, container watering, and anti-frost misting, all while managing water resources and considering weather forecasts.

## Features

- **Multiple Irrigation Types**:
  - Classic Irrigation (soil moisture-based)
  - Container Irrigation (volume-based with overflow protection)
  - Anti-Frost Misting (temperature-based protection)
- **Rain Water Management**: Tracks available rainwater and prevents pump damage
- **Weather Integration**: Considers rain predictions to avoid unnecessary watering
- **Smart Scheduling**: Time windows and season-aware operation
- **Pump Protection**: Flow rate management and minimum available water requirements
- **Multi-Zone Support**: Configure different zones with individual settings
- **Notifications**: Alert when issues occur (low water, overflows, etc.)

## Configuration

The Smart Irrigation app is configured using the `SmartIrrigationConfig` class in your `appsettings.json`.

### Basic Configuration Structure

```json
{
  "SmartIrrigation": {
    "PumpSocketEntity": "switch.irrigation_pump",
    "PumpFlowRate": 2000,
    "AvailableRainWaterEntity": "sensor.rain_water_tank_volume",
    "MinimumAvailableRainWater": 100,
    "WeatherEntity": "weather.home",
    "RainPredictionLiters": 5.0,
    "RainPredictionDays": 2,
    "PhoneToNotify": "mobile_app_iphone",
    "Zones": [
      {
        "Name": "Front Lawn",
        "FlowRate": 600,
        "ValveEntity": "switch.irrigation_valve_zone_1",
        "IrrigationSeasonStart": "04-01",
        "IrrigationSeasonEnd": "10-31",
        "Irrigation": {
          "SoilMoistureEntity": "sensor.front_lawn_soil_moisture",
          "CriticalSoilMoisture": 15,
          "LowSoilMoisture": 25,
          "TargetSoilMoisture": 40,
          "MaxDuration": "00:30:00",
          "MinimumTimeout": "08:00:00",
          "IrrigationStartWindow": "06:00:00",
          "IrrigationEndWindow": "09:00:00"
        }
      }
    ]
  }
}
```

### Main Configuration Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `PumpSocketEntity` | string | Yes | Switch entity controlling the irrigation pump |
| `PumpFlowRate` | int | Yes | Total pump flow rate in liters per hour |
| `AvailableRainWaterEntity` | string | Yes | Sensor showing available rainwater volume (liters) |
| `MinimumAvailableRainWater` | int | Yes | Minimum water level required to run pump (liters) |
| `WeatherEntity` | string | Yes | Weather forecast entity |
| `RainPredictionLiters` | double | No | Expected rain amount to skip irrigation (mm) |
| `RainPredictionDays` | int | No | Days ahead to check rain forecast |
| `PhoneToNotify` | string | Yes | Mobile app entity for notifications |
| `Zones` | array | Yes | Array of irrigation zone configurations |

### Zone Configuration

Each zone can have one of three irrigation types: Classic (soil-based), Container (volume-based), or Anti-Frost Misting.

#### Common Zone Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Name` | string | Yes | Friendly name for the zone |
| `FlowRate` | int | Yes | Water flow rate for this zone (liters/hour) |
| `ValveEntity` | string | Yes | Switch entity controlling the zone valve |
| `IrrigationSeasonStart` | string | No | Start date for irrigation season (MM-DD format) |
| `IrrigationSeasonEnd` | string | No | End date for irrigation season (MM-DD format) |
| `Irrigation` | object | No | Classic irrigation configuration |
| `Container` | object | No | Container irrigation configuration |
| `AntiFrostMisting` | object | No | Anti-frost misting configuration |

**Note**: Each zone should have exactly one irrigation type configured (Irrigation, Container, or AntiFrostMisting).

### Classic Irrigation Configuration

For traditional garden beds and lawns based on soil moisture.

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `SoilMoistureEntity` | string | Yes | Sensor showing soil moisture percentage |
| `CriticalSoilMoisture` | int | Yes | Critical moisture level (immediate watering needed) |
| `LowSoilMoisture` | int | Yes | Low moisture level (watering needed) |
| `TargetSoilMoisture` | int | Yes | Target moisture level to reach |
| `MaxDuration` | TimeSpan | No | Maximum watering duration per session |
| `MinimumTimeout` | TimeSpan | No | Minimum time between watering sessions |
| `IrrigationStartWindow` | TimeOnly | No | Earliest time to start watering (HH:mm:ss) |
| `IrrigationEndWindow` | TimeOnly | No | Latest time to start watering (HH:mm:ss) |

**Example**:
```json
{
  "Name": "Front Lawn",
  "FlowRate": 600,
  "ValveEntity": "switch.valve_front_lawn",
  "IrrigationSeasonStart": "04-01",
  "IrrigationSeasonEnd": "10-31",
  "Irrigation": {
    "SoilMoistureEntity": "sensor.front_lawn_moisture",
    "CriticalSoilMoisture": 15,
    "LowSoilMoisture": 25,
    "TargetSoilMoisture": 40,
    "MaxDuration": "00:30:00",
    "MinimumTimeout": "08:00:00",
    "IrrigationStartWindow": "06:00:00",
    "IrrigationEndWindow": "09:00:00"
  }
}
```

### Container Irrigation Configuration

For potted plants and containers with volume/weight sensors and overflow detection.

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `VolumeEntity` | string | Yes | Sensor showing container water volume/weight |
| `CriticalVolume` | int | Yes | Critical volume (immediate watering needed) |
| `LowVolume` | int | Yes | Low volume (watering needed) |
| `TargetVolume` | int | Yes | Target volume to reach |
| `OverFlowEntity` | string | Yes | Binary sensor detecting overflow/full condition |

**Example**:
```json
{
  "Name": "Patio Planters",
  "FlowRate": 200,
  "ValveEntity": "switch.valve_patio",
  "IrrigationSeasonStart": "03-15",
  "IrrigationSeasonEnd": "11-15",
  "Container": {
    "VolumeEntity": "sensor.planter_weight",
    "CriticalVolume": 500,
    "LowVolume": 800,
    "TargetVolume": 1200,
    "OverFlowEntity": "binary_sensor.planter_overflow"
  }
}
```

### Anti-Frost Misting Configuration

For frost protection using periodic misting.

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `TemperatureEntity` | string | Yes | Temperature sensor for the protected area |
| `CriticalTemperature` | double | Yes | Temperature requiring immediate misting (°C) |
| `LowTemperature` | double | Yes | Temperature requiring preventive misting (°C) |
| `MistingDuration` | TimeSpan | Yes | Duration of each misting cycle |
| `MistingTimeout` | TimeSpan | Yes | Time between misting cycles |

**Example**:
```json
{
  "Name": "Fruit Trees",
  "FlowRate": 400,
  "ValveEntity": "switch.valve_orchard",
  "IrrigationSeasonStart": "03-01",
  "IrrigationSeasonEnd": "05-31",
  "AntiFrostMisting": {
    "TemperatureEntity": "sensor.orchard_temperature",
    "CriticalTemperature": -2.0,
    "LowTemperature": 1.0,
    "MistingDuration": "00:05:00",
    "MistingTimeout": "00:30:00"
  }
}
```

## Complete Example Configuration

```json
{
  "SmartIrrigation": {
    "PumpSocketEntity": "switch.irrigation_pump",
    "PumpFlowRate": 2000,
    "AvailableRainWaterEntity": "sensor.rain_tank_volume",
    "MinimumAvailableRainWater": 100,
    "WeatherEntity": "weather.home",
    "RainPredictionLiters": 5.0,
    "RainPredictionDays": 2,
    "PhoneToNotify": "mobile_app_my_phone",
    "Zones": [
      {
        "Name": "Front Lawn",
        "FlowRate": 600,
        "ValveEntity": "switch.valve_zone_1",
        "IrrigationSeasonStart": "04-01",
        "IrrigationSeasonEnd": "10-31",
        "Irrigation": {
          "SoilMoistureEntity": "sensor.front_lawn_moisture",
          "CriticalSoilMoisture": 15,
          "LowSoilMoisture": 25,
          "TargetSoilMoisture": 40,
          "MaxDuration": "00:30:00",
          "MinimumTimeout": "08:00:00",
          "IrrigationStartWindow": "06:00:00",
          "IrrigationEndWindow": "09:00:00"
        }
      },
      {
        "Name": "Back Garden",
        "FlowRate": 800,
        "ValveEntity": "switch.valve_zone_2",
        "IrrigationSeasonStart": "04-01",
        "IrrigationSeasonEnd": "10-31",
        "Irrigation": {
          "SoilMoistureEntity": "sensor.back_garden_moisture",
          "CriticalSoilMoisture": 20,
          "LowSoilMoisture": 30,
          "TargetSoilMoisture": 45,
          "MaxDuration": "00:45:00",
          "MinimumTimeout": "12:00:00",
          "IrrigationStartWindow": "05:30:00",
          "IrrigationEndWindow": "08:30:00"
        }
      },
      {
        "Name": "Balcony Planters",
        "FlowRate": 200,
        "ValveEntity": "switch.valve_zone_3",
        "IrrigationSeasonStart": "03-15",
        "IrrigationSeasonEnd": "11-15",
        "Container": {
          "VolumeEntity": "sensor.balcony_planter_weight",
          "CriticalVolume": 400,
          "LowVolume": 700,
          "TargetVolume": 1000,
          "OverFlowEntity": "binary_sensor.balcony_overflow"
        }
      },
      {
        "Name": "Greenhouse",
        "FlowRate": 400,
        "ValveEntity": "switch.valve_zone_4",
        "IrrigationSeasonStart": "02-15",
        "IrrigationSeasonEnd": "05-15",
        "AntiFrostMisting": {
          "TemperatureEntity": "sensor.greenhouse_temperature",
          "CriticalTemperature": -2.0,
          "LowTemperature": 1.0,
          "MistingDuration": "00:05:00",
          "MistingTimeout": "00:30:00"
        }
      }
    ]
  }
}
```

## How It Works

### Classic Irrigation Logic

1. **Check Season**: Only operates within configured irrigation season
2. **Check Rain Forecast**: Skips irrigation if significant rain is predicted
3. **Monitor Soil Moisture**: Continuously tracks moisture levels
4. **Determine Priority**:
   - Critical: Moisture ? CriticalSoilMoisture ? Water immediately
   - Low: Moisture ? LowSoilMoisture ? Water during time window
   - Target: Moisture ? TargetSoilMoisture ? Water if excess capacity available
5. **Check Water Availability**: Ensures sufficient rainwater is available
6. **Check Time Window**: Respects irrigation start/end times
7. **Irrigate**: Opens valve and runs pump until target reached or max duration elapsed
8. **Timeout**: Enforces minimum timeout between sessions

### Container Irrigation Logic

1. **Check Season**: Only operates within configured irrigation season
2. **Monitor Volume/Weight**: Continuously tracks container levels
3. **Check Overflow**: Stops immediately if overflow detected
4. **Determine Priority**:
   - Critical: Volume ? CriticalVolume ? Water immediately
   - Low: Volume ? LowVolume ? Water when possible
   - Target: Volume ? TargetVolume ? Water if capacity available
5. **Check Water Availability**: Ensures sufficient rainwater is available
6. **Irrigate**: Opens valve and runs pump until target reached or overflow detected

### Anti-Frost Misting Logic

1. **Check Season**: Only operates within configured season
2. **Monitor Temperature**: Continuously tracks temperature
3. **Determine Action**:
   - Temperature ? CriticalTemperature ? Start misting immediately
   - Temperature ? LowTemperature ? Start preventive misting
4. **Misting Cycle**:
   - Run for configured `MistingDuration`
   - Wait for configured `MistingTimeout`
   - Repeat while temperature remains low
5. **Check Water Availability**: Ensures sufficient rainwater is available

### Rain Prediction

If `RainPredictionLiters` and `RainPredictionDays` are configured, the system will:
- Check weather forecast for the next N days
- Skip irrigation if predicted rain exceeds threshold
- Saves water and prevents overwatering

### Pump and Flow Management

- Only one zone can run at a time to respect pump flow rate
- Pump automatically turns on when any valve opens
- Pump automatically turns off when all valves close
- Zone flow rates must sum to less than or equal to pump flow rate
- System prevents pump operation if water level is too low

## Notifications

The app sends notifications for:
- Low rainwater level preventing irrigation
- Container overflow detected
- Irrigation season start/end
- Critical conditions requiring immediate action

## Seasonal Operation

Use `IrrigationSeasonStart` and `IrrigationSeasonEnd` to define when each zone should be active:
- Format: "MM-DD" (e.g., "04-01" for April 1st)
- Zones automatically disable outside their season
- Useful for different plant types with varying growing seasons

## Tips

- **Soil Moisture Sensors**: Calibrate sensors for your soil type (sandy vs. clay affects readings)
- **Container Sensors**: Weight-based sensors are more reliable than capacitive moisture sensors
- **Flow Rates**: Measure actual flow rates for accurate watering duration calculations
- **Time Windows**: Water early morning (6-9 AM) to minimize evaporation
- **Anti-Frost**: Enable during spring when late frosts can damage blossoms
- **Rain Threshold**: Adjust based on local climate (5mm works well for moderate climates)
- **Pump Protection**: Set `MinimumAvailableRainWater` to ~5-10% of tank capacity

## Integration with Energy Manager

For solar-powered pump systems, the irrigation zones can be configured as energy consumers in the Energy Manager to optimize pump operation during peak solar production.
