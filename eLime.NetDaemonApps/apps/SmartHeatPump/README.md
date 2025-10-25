# Smart Heat Pump

The Smart Heat Pump app intelligently manages a heat pump system with Smart Grid Ready (SGR) functionality. It monitors room and hot water temperatures, calculates energy demands, manages hot water temperature setpoints, and controls SGR modes for optimal energy efficiency.

## Features

- **Energy Demand Management**: Automatically calculates energy demands for room heating and hot water
- **Smart Grid Ready Integration**: Controls SGR inputs for dynamic energy management
- **Hot Water Temperature Control**: Dynamically adjusts maximum hot water temperature based on heating priorities
- **Shower/Bath Requests**: Handles special hot water requests with higher priority
- **COP Monitoring**: Tracks Coefficient of Performance for both heating and hot water
- **Source Temperature Tracking**: Monitors and tracks heat source temperature
- **ISG Integration**: Communicates with Stiebel Eltron ISG (Internet Service Gateway) via HTTP

## Configuration

The Smart Heat Pump is configured using the `SmartHeatPumpConfig` class in your `appsettings.json`.

### Basic Configuration Structure

```json
{
  "SmartHeatPump": {
    "SmartGridReadyInput1": "input_boolean.heat_pump_sgr_input_1",
    "SmartGridReadyInput2": "input_boolean.heat_pump_sgr_input_2",
    "SourcePumpRunningSensor": "binary_sensor.heat_pump_source_pump_running",
    "SourceTemperatureSensor": "sensor.heat_pump_source_temperature",
    "IsSummerModeSensor": "binary_sensor.heat_pump_summer_mode",
    "IsCoolingSensor": "binary_sensor.heat_pump_cooling",
    "StatusBytesSensor": "sensor.heat_pump_status_bytes",
    "RemainingStandstillSensor": "sensor.heat_pump_remaining_standstill",
    "HeatConsumedTodayIntegerSensor": "sensor.heat_consumed_today_integer",
    "HeatConsumedTodayDecimalsSensor": "sensor.heat_consumed_today_decimals",
    "HeatProducedTodayIntegerSensor": "sensor.heat_produced_today_integer",
    "HeatProducedTodayDecimalsSensor": "sensor.heat_produced_today_decimals",
    "HotWaterConsumedTodayIntegerSensor": "sensor.hot_water_consumed_today_integer",
    "HotWaterConsumedTodayDecimalsSensor": "sensor.hot_water_consumed_today_decimals",
    "HotWaterProducedTodayIntegerSensor": "sensor.hot_water_produced_today_integer",
    "HotWaterProducedTodayDecimalsSensor": "sensor.hot_water_produced_today_decimals",
    "IsgBaseUrl": "http://192.168.1.100",
    "Temperatures": {
      "RoomTemperatureSensor": "sensor.living_room_temperature",
      "MinimumRoomTemperature": 19.5,
      "ComfortRoomTemperature": 20.5,
      "MaximumRoomTemperature": 21.5,
      "HotWaterTemperatureSensor": "sensor.hot_water_temperature",
      "MinimumHotWaterTemperature": 45.0,
      "ComfortHotWaterTemperature": 50.0,
      "MaximumHotWaterTemperature": 54.0,
      "TargetShowerTemperature": 51.0,
      "TargetBathTemperature": 54.0
    }
  }
}
```

### Configuration Properties

#### Main Configuration

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `SmartGridReadyInput1` | string | Yes | Entity for SGR input 1 (boolean input) |
| `SmartGridReadyInput2` | string | Yes | Entity for SGR input 2 (boolean input) |
| `SourcePumpRunningSensor` | string | Yes | Binary sensor indicating if source pump is running |
| `SourceTemperatureSensor` | string | Yes | Sensor for heat source temperature (e.g., ground/air) |
| `IsSummerModeSensor` | string | Yes | Binary sensor indicating summer mode status |
| `IsCoolingSensor` | string | Yes | Binary sensor indicating cooling mode status |
| `StatusBytesSensor` | string | Yes | Sensor with heat pump status information |
| `RemainingStandstillSensor` | string | Yes | Sensor showing remaining standstill time in minutes |
| `HeatConsumedTodayIntegerSensor` | string | Yes | Integer part of heat consumed today (kWh) |
| `HeatConsumedTodayDecimalsSensor` | string | Yes | Decimal part of heat consumed today |
| `HeatProducedTodayIntegerSensor` | string | Yes | Integer part of heat produced today (kWh) |
| `HeatProducedTodayDecimalsSensor` | string | Yes | Decimal part of heat produced today |
| `HotWaterConsumedTodayIntegerSensor` | string | Yes | Integer part of hot water energy consumed today (kWh) |
| `HotWaterConsumedTodayDecimalsSensor` | string | Yes | Decimal part of hot water energy consumed today |
| `HotWaterProducedTodayIntegerSensor` | string | Yes | Integer part of hot water energy produced today (kWh) |
| `HotWaterProducedTodayDecimalsSensor` | string | Yes | Decimal part of hot water energy produced today |
| `IsgBaseUrl` | string | Yes | Base URL of the ISG (Internet Service Gateway) |
| `Temperatures` | object | Yes | Temperature configuration (see below) |

#### Temperature Configuration

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `RoomTemperatureSensor` | string | Yes | Sensor for room/indoor temperature |
| `MinimumRoomTemperature` | decimal | Yes | Minimum acceptable room temperature (°C) |
| `ComfortRoomTemperature` | decimal | Yes | Comfort room temperature threshold (°C) |
| `MaximumRoomTemperature` | decimal | Yes | Maximum desired room temperature (°C) |
| `HotWaterTemperatureSensor` | string | Yes | Sensor for hot water temperature |
| `MinimumHotWaterTemperature` | decimal | Yes | Minimum acceptable hot water temperature (°C) |
| `ComfortHotWaterTemperature` | decimal | Yes | Comfort hot water temperature threshold (°C) |
| `MaximumHotWaterTemperature` | decimal | Yes | Maximum hot water temperature (°C) |
| `TargetShowerTemperature` | decimal | Yes | Target temperature for shower requests (°C) |
| `TargetBathTemperature` | decimal | Yes | Target temperature for bath requests (°C) |

### Smart Grid Ready Modes

The heat pump can operate in four SGR modes based on input combinations:

| Mode | Input 1 | Input 2 | Description |
|------|---------|---------|-------------|
| **Blocked** | OFF | ON | Heat pump operation blocked/reduced |
| **Normal** | OFF | OFF | Normal operation mode |
| **Boosted** | ON | OFF | Enhanced operation (extra energy available) |
| **Maximized** | ON | ON | Maximum operation (excess energy available) |

### Energy Demand Levels

The app calculates energy demand based on temperature readings:

- **CriticalDemand**: Temperature significantly below target, priority operation needed
- **Demanded**: Temperature below comfort level, operation desired
- **CanUse**: Temperature acceptable but can use extra energy if available
- **NoDemand**: Temperature at or above maximum, no energy needed

### MQTT Sensors

The app creates several MQTT sensors for integration and monitoring:

- `sensor.smart_heat_pump_energy_demand`: Overall energy demand state
- `sensor.smart_heat_pump_room_energy_demand`: Room heating energy demand
- `sensor.smart_heat_pump_hot_water_energy_demand`: Hot water energy demand
- `sensor.smart_heat_pump_source_temperature`: Heat source temperature
- `sensor.smart_heat_pump_maximum_hot_water_temperature`: Current max hot water setpoint
- `sensor.smart_heat_pump_heat_cop`: Coefficient of Performance for heating
- `sensor.smart_heat_pump_hot_water_cop`: Coefficient of Performance for hot water
- `sensor.smart_heat_pump_expected_power_consumption`: Expected power consumption
- `select.smart_heat_pump_sgr_mode`: Smart Grid Ready mode selector
- `button.smart_heat_pump_shower_request`: Trigger shower preparation
- `button.smart_heat_pump_bath_request`: Trigger bath preparation
- `number.smart_heat_pump_max_hot_water_temp`: Adjust maximum hot water temperature

## Complete Example Configuration

```json
{
  "SmartHeatPump": {
    "SmartGridReadyInput1": "input_boolean.wp_sgr_input_1",
    "SmartGridReadyInput2": "input_boolean.wp_sgr_input_2",
    "SourcePumpRunningSensor": "binary_sensor.wp_source_pump",
    "SourceTemperatureSensor": "sensor.wp_source_temperature",
    "IsSummerModeSensor": "binary_sensor.wp_summer_mode",
    "IsCoolingSensor": "binary_sensor.wp_cooling",
    "StatusBytesSensor": "sensor.wp_status_bytes",
    "RemainingStandstillSensor": "sensor.wp_standstill_remaining",
    "HeatConsumedTodayIntegerSensor": "sensor.wp_heat_consumed_int",
    "HeatConsumedTodayDecimalsSensor": "sensor.wp_heat_consumed_dec",
    "HeatProducedTodayIntegerSensor": "sensor.wp_heat_produced_int",
    "HeatProducedTodayDecimalsSensor": "sensor.wp_heat_produced_dec",
    "HotWaterConsumedTodayIntegerSensor": "sensor.wp_hw_consumed_int",
    "HotWaterConsumedTodayDecimalsSensor": "sensor.wp_hw_consumed_dec",
    "HotWaterProducedTodayIntegerSensor": "sensor.wp_hw_produced_int",
    "HotWaterProducedTodayDecimalsSensor": "sensor.wp_hw_produced_dec",
    "IsgBaseUrl": "http://192.168.1.50",
    "Temperatures": {
      "RoomTemperatureSensor": "sensor.living_room_temperature",
      "MinimumRoomTemperature": 19.5,
      "ComfortRoomTemperature": 20.5,
      "MaximumRoomTemperature": 21.5,
      "HotWaterTemperatureSensor": "sensor.hot_water_tank_temperature",
      "MinimumHotWaterTemperature": 45.0,
      "ComfortHotWaterTemperature": 50.0,
      "MaximumHotWaterTemperature": 54.0,
      "TargetShowerTemperature": 51.0,
      "TargetBathTemperature": 54.0
    }
  }
}
```

## Usage

Once configured, the Smart Heat Pump app will:

1. **Monitor Temperatures**: Continuously track room and hot water temperatures
2. **Calculate Energy Demands**: Determine heating and hot water priorities based on temperature thresholds
3. **Manage SGR Modes**: Automatically adjust Smart Grid Ready mode (can also be manually controlled via MQTT select entity)
4. **Control Hot Water Temperature**: Dynamically adjust maximum hot water temperature based on heating priorities
5. **Handle Special Requests**: Process shower/bath requests with elevated priority
6. **Track Performance**: Calculate and publish COP values for heating and hot water
7. **Monitor Source**: Track heat source temperature for efficiency analysis

### Requesting Shower or Bath

Use the MQTT button entities to request hot water preparation:

- Press `button.smart_heat_pump_shower_request` before showering
- Press `button.smart_heat_pump_bath_request` before taking a bath

The system will prioritize heating water to the target temperature (51°C for shower, 54°C for bath) and automatically clear the request once reached or after 3 hours.

### Manual SGR Mode Control

You can manually override the SGR mode using:
```yaml
service: select.select_option
target:
  entity_id: select.smart_heat_pump_sgr_mode
data:
  option: "Boosted"  # Options: Blocked, Normal, Boosted, Maximized
```

## Integration with Energy Manager

This app is designed to work seamlessly with the Energy Manager app. The Energy Manager can read the energy demand and expected power consumption to intelligently schedule heat pump operation based on available solar power or grid conditions.
