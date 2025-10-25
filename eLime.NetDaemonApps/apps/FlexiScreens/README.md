# FlexiScreens

The FlexiScreens app provides intelligent automation for window screens, blinds, and shutters. It automatically manages screens based on sun position, storm protection, temperature control, and sleep schedules while respecting manual overrides.

## Features

- **Sun Protection**: Automatically lower/raise screens based on sun position and orientation
- **Storm Protection**: Protect screens from damage during high winds and rain
- **Temperature Protection**: Control screens to prevent overheating or assist cooling
- **Sleep Mode Integration**: Adjust screens based on sleep schedules
- **Manual Override Respect**: Honors manual adjustments with configurable timeout periods
- **Per-Screen Configuration**: Individual settings for each screen based on orientation and room purpose

## Configuration

The FlexiScreens app is configured using the `FlexiScreensConfig` class in your `appsettings.json`.

### Basic Configuration Structure

```json
{
  "FlexiScreens": {
    "NetDaemonUserId": "netdaemon_user_id",
    "Screens": {
      "living_room_south": {
        "Name": "Living Room South",
        "Enabled": true,
        "ScreenEntity": "cover.living_room_south_screen",
        "Orientation": 180,
        "SleepSensor": "binary_sensor.family_sleeping",
        "MinimumIntervalSinceLastAutomatedAction": "00:10:00",
        "MinimumIntervalSinceLastManualAction": "01:00:00",
        "SunProtection": {
          "SunEntity": "sun.sun",
          "OrientationThreshold": 45,
          "ElevationThreshold": 30,
          "DesiredStateBelowElevationThreshold": "Up"
        },
        "StormProtection": {
          "WindSpeedEntity": "sensor.wind_speed",
          "WindSpeedStormStartThreshold": 50,
          "WindSpeedStormEndThreshold": 40,
          "RainRateEntity": "sensor.rain_rate",
          "RainRateStormStartThreshold": 10,
          "RainRateStormEndThreshold": 5
        },
        "TemperatureProtection": {
          "SolarLuxSensor": "sensor.solar_lux",
          "SolarLuxAboveThreshold": 40000,
          "SolarLuxBelowThreshold": 35000,
          "IndoorTemperatureSensor": "sensor.living_room_temperature",
          "MaxIndoorTemperature": 24.5,
          "IsCoolingEntity": "binary_sensor.ac_cooling"
        }
      }
    }
  }
}
```

### Main Configuration Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `NetDaemonUserId` | string | Yes | User ID for NetDaemon (for distinguishing automated vs manual actions) |
| `Screens` | Dictionary | Yes | Collection of screen configurations, keyed by unique identifier |

### Screen Configuration Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Name` | string | Yes | Friendly name for the screen |
| `Enabled` | bool | No | Enable/disable automation for this screen (default: true) |
| `ScreenEntity` | string | Yes | Home Assistant cover entity ID |
| `Orientation` | int | Yes | Screen orientation in degrees (0=North, 90=East, 180=South, 270=West) |
| `SleepSensor` | string | No | Binary sensor indicating sleep/wake status |
| `MinimumIntervalSinceLastAutomatedAction` | TimeSpan | No | Minimum time between automated actions (default: 10 minutes) |
| `MinimumIntervalSinceLastManualAction` | TimeSpan | No | Time to wait after manual action before resuming automation (default: 1 hour) |
| `SunProtection` | object | Yes | Sun protection configuration |
| `StormProtection` | object | No | Storm protection configuration |
| `TemperatureProtection` | object | No | Temperature-based protection configuration |

### Sun Protection Configuration

Controls screen behavior based on sun position relative to screen orientation.

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `SunEntity` | string | Yes | Sun entity (typically `sun.sun`) |
| `OrientationThreshold` | double | No | Degrees from screen orientation where sun protection activates (±45°) |
| `ElevationThreshold` | double | No | Minimum sun elevation angle to trigger sun protection (degrees) |
| `DesiredStateBelowElevationThreshold` | string | Yes | Action when sun is below elevation threshold: "Up" or "Down" |

**Example**: For a south-facing window (Orientation: 180):
- With `OrientationThreshold: 45`, protection activates when sun azimuth is between 135° and 225°
- With `ElevationThreshold: 30`, protection only activates when sun is above 30° elevation
- `DesiredStateBelowElevationThreshold: "Up"` means screen goes up when sun is too low

### Storm Protection Configuration

Protects screens during adverse weather conditions.

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `WindSpeedEntity` | string | No | Sensor for wind speed (km/h or mph) |
| `WindSpeedStormStartThreshold` | double | No | Wind speed to trigger storm mode |
| `WindSpeedStormEndThreshold` | double | No | Wind speed to exit storm mode (hysteresis) |
| `RainRateEntity` | string | No | Sensor for rain rate (mm/h) |
| `RainRateStormStartThreshold` | double | No | Rain rate to trigger storm mode |
| `RainRateStormEndThreshold` | double | No | Rain rate to exit storm mode (hysteresis) |
| `ShortTermRainForecastEntity` | string | No | Sensor for short-term rain forecast |
| `ShortTermRainStormStartThreshold` | double | No | Forecast rain to trigger storm mode |
| `ShortTermRainStormEndThreshold` | double | No | Forecast rain to exit storm mode |
| `HourlyWeatherEntity` | string | No | Hourly weather forecast entity |
| `NightlyPredictionHours` | int | No | Hours to look ahead for nightly storm prediction |
| `NightlyWindSpeedThreshold` | double | No | Wind speed threshold for nightly storm |
| `NightlyRainThreshold` | double | No | Total rain threshold for nightly storm |
| `NightlyRainRateThreshold` | double | No | Rain rate threshold for nightly storm |

During storm conditions, screens will be raised to prevent damage.

### Temperature Protection Configuration

Manages screens to control indoor temperature and prevent overheating.

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `SolarLuxSensor` | string | No | Sensor for solar irradiance (lux) |
| `SolarLuxAboveThreshold` | double | No | Lux level to trigger temperature protection |
| `SolarLuxBelowThreshold` | double | No | Lux level to exit temperature protection (hysteresis) |
| `IndoorTemperatureSensor` | string | No | Indoor temperature sensor |
| `MaxIndoorTemperature` | double | No | Maximum desired indoor temperature (°C) |
| `IsCoolingEntity` | string | No | Binary sensor indicating if cooling is active |
| `WeatherEntity` | string | No | Weather forecast entity |
| `ConditionalMaxIndoorTemperature` | double | No | Alternative max temperature threshold |
| `ConditionalOutdoorTemperaturePrediction` | double | No | Outdoor temperature to use conditional threshold |
| `ConditionalOutdoorTemperaturePredictionDays` | int | No | Days ahead to check for conditional threshold |

Temperature protection lowers screens when:
- Solar irradiance is high AND
- Indoor temperature exceeds threshold OR
- Outdoor temperature prediction indicates hot weather

## Screen Actions

The app determines the appropriate action for each screen:

- **Up**: Raise the screen (fully open)
- **Down**: Lower the screen (fully closed)
- **None**: No action needed

## Complete Example Configuration

### Living Room (South-Facing, Sun Protection Priority)

```json
{
  "Name": "Living Room South",
  "Enabled": true,
  "ScreenEntity": "cover.living_room_south",
  "Orientation": 180,
  "SleepSensor": "binary_sensor.daytime",
  "MinimumIntervalSinceLastAutomatedAction": "00:15:00",
  "MinimumIntervalSinceLastManualAction": "02:00:00",
  "SunProtection": {
    "SunEntity": "sun.sun",
    "OrientationThreshold": 45,
    "ElevationThreshold": 25,
    "DesiredStateBelowElevationThreshold": "Up"
  },
  "StormProtection": {
    "WindSpeedEntity": "sensor.wind_speed",
    "WindSpeedStormStartThreshold": 50,
    "WindSpeedStormEndThreshold": 40,
    "RainRateEntity": "sensor.rain_rate",
    "RainRateStormStartThreshold": 10,
    "RainRateStormEndThreshold": 5,
    "HourlyWeatherEntity": "weather.hourly_forecast",
    "NightlyPredictionHours": 12,
    "NightlyWindSpeedThreshold": 60,
    "NightlyRainThreshold": 20,
    "NightlyRainRateThreshold": 5
  },
  "TemperatureProtection": {
    "SolarLuxSensor": "sensor.solar_irradiance",
    "SolarLuxAboveThreshold": 40000,
    "SolarLuxBelowThreshold": 30000,
    "IndoorTemperatureSensor": "sensor.living_room_temperature",
    "MaxIndoorTemperature": 24.0,
    "IsCoolingEntity": "binary_sensor.ac_active",
    "WeatherEntity": "weather.forecast",
    "ConditionalMaxIndoorTemperature": 23.0,
    "ConditionalOutdoorTemperaturePrediction": 30.0,
    "ConditionalOutdoorTemperaturePredictionDays": 2
  }
}
```

### Bedroom (North-Facing, Privacy/Sleep Priority)

```json
{
  "Name": "Bedroom North",
  "Enabled": true,
  "ScreenEntity": "cover.bedroom_north",
  "Orientation": 0,
  "SleepSensor": "binary_sensor.bedroom_sleeping",
  "MinimumIntervalSinceLastAutomatedAction": "00:20:00",
  "MinimumIntervalSinceLastManualAction": "03:00:00",
  "SunProtection": {
    "SunEntity": "sun.sun",
    "OrientationThreshold": 30,
    "ElevationThreshold": 15,
    "DesiredStateBelowElevationThreshold": "Down"
  },
  "StormProtection": {
    "WindSpeedEntity": "sensor.wind_speed",
    "WindSpeedStormStartThreshold": 50,
    "WindSpeedStormEndThreshold": 40
  }
}
```

### Office (East-Facing, Morning Sun Protection)

```json
{
  "Name": "Office East",
  "Enabled": true,
  "ScreenEntity": "cover.office_east",
  "Orientation": 90,
  "MinimumIntervalSinceLastAutomatedAction": "00:10:00",
  "MinimumIntervalSinceLastManualAction": "01:00:00",
  "SunProtection": {
    "SunEntity": "sun.sun",
    "OrientationThreshold": 50,
    "ElevationThreshold": 20,
    "DesiredStateBelowElevationThreshold": "Up"
  },
  "StormProtection": {
    "WindSpeedEntity": "sensor.wind_speed",
    "WindSpeedStormStartThreshold": 55,
    "WindSpeedStormEndThreshold": 45,
    "ShortTermRainForecastEntity": "sensor.rain_forecast_1h",
    "ShortTermRainStormStartThreshold": 5,
    "ShortTermRainStormEndThreshold": 2
  },
  "TemperatureProtection": {
    "SolarLuxSensor": "sensor.solar_lux",
    "SolarLuxAboveThreshold": 35000,
    "SolarLuxBelowThreshold": 25000,
    "IndoorTemperatureSensor": "sensor.office_temperature",
    "MaxIndoorTemperature": 25.0
  }
}
```

## Usage

Once configured, the FlexiScreens app will:

1. **Monitor Conditions**: Continuously track sun position, weather conditions, and indoor temperature
2. **Calculate Required Actions**: Determine optimal screen position based on all configured protections
3. **Respect Manual Control**: Wait for configured timeout period after manual adjustments
4. **Execute Actions**: Automatically raise or lower screens as needed
5. **Prevent Rapid Changes**: Enforce minimum intervals between automated actions

### Manual Override

When you manually adjust a screen:
- Automation pauses for the configured `MinimumIntervalSinceLastManualAction` duration
- This allows you to maintain manual control when needed
- After the timeout expires, automation resumes based on current conditions

### Priority Order

The app evaluates protections in this order:
1. **Storm Protection**: Highest priority - always raises screens during storms
2. **Sleep Mode**: Second priority - respects sleep schedules
3. **Temperature Protection**: Third priority - prevents overheating
4. **Sun Protection**: Base priority - manages daily sun exposure

## Tips

- **Orientation Values**: 0=North, 45=NE, 90=East, 135=SE, 180=South, 225=SW, 270=West, 315=NW
- **Hysteresis**: Use different start/end thresholds to prevent rapid toggling
- **Sleep Sensors**: Can be template sensors combining time, occupancy, and day-of-week logic
- **Testing**: Start with longer `MinimumIntervalSinceLastAutomatedAction` values and adjust based on behavior
- **Manual Override Period**: Set longer periods for rooms where manual control is frequently needed

## NetDaemon User ID

The `NetDaemonUserId` is used to distinguish between automated actions (by NetDaemon) and manual actions (by users). This can be found in Home Assistant under Settings ? People ? NetDaemon user.
