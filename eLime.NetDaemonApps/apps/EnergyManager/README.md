# Energy Manager

The Energy Manager app intelligently manages energy consumers (devices, appliances, etc.) based on solar production, grid capacity, battery state, and time windows. It optimizes energy usage by automatically turning devices on/off based on available solar power and grid conditions.

## Features

- **Smart Grid Management**: Monitors grid voltage, import/export, and peak demand
- **Solar Power Optimization**: Uses solar production forecasts to optimize device scheduling
- **Multiple Consumer Types**: Supports various consumer types including simple switches, cooling systems, triggered devices, Smart Grid Ready appliances, and car chargers
- **Battery Management**: Integrates with home battery systems for optimal energy storage
- **Time Window Scheduling**: Schedule devices to run during specific time windows
- **Dynamic Load Balancing**: Adjusts device operation based on different balancing methods
- **Consumer Groups**: Organize and manage multiple consumers together

## Configuration

The Energy Manager is configured using the `EnergyManagerConfig` class in your `appsettings.json`.

### Basic Configuration Structure

```json
{
  "EnergyManager": {
    "Timezone": "Europe/Brussels",
    "Grid": {
      "VoltageEntity": "sensor.grid_voltage",
      "ImportEntity": "sensor.grid_import_power",
      "ExportEntity": "sensor.grid_export_power",
      "PeakImportEntity": "sensor.peak_import_today",
      "CurrentAverageDemandEntity": "sensor.current_average_demand",
      "CurrentSolarPowerEntity": "sensor.solar_power",
      "SolarForecastPowerNowEntity": "sensor.solar_forecast_now",
      "SolarForecastPower30MinutesEntity": "sensor.solar_forecast_30min",
      "SolarForecastPower1HourEntity": "sensor.solar_forecast_1hour"
    },
    "SolarProductionRemainingTodayEntity": "sensor.solar_remaining_today",
    "PhoneToNotify": "mobile_app_iphone",
    "BatteryManager": {
      "Batteries": [
        {
          "Name": "Home Battery",
          "ChargeEntity": "sensor.battery_charge",
          "DischargeEntity": "sensor.battery_discharge",
          "StateOfChargeEntity": "sensor.battery_soc",
          "MinimumStateOfCharge": 20,
          "MaximumStateOfCharge": 95,
          "Capacity": 10000
        }
      ]
    },
    "Consumers": [
      // See consumer examples below
    ]
  }
}
```

### Grid Configuration Properties

| Property | Type | Description |
|----------|------|-------------|
| `VoltageEntity` | string | Entity tracking grid voltage |
| `ImportEntity` | string | Entity tracking power imported from grid |
| `ExportEntity` | string | Entity tracking power exported to grid |
| `PeakImportEntity` | string | Entity tracking peak import demand |
| `CurrentAverageDemandEntity` | string | Entity tracking current average power demand |
| `CurrentSolarPowerEntity` | string | Entity tracking current solar production |
| `SolarForecastPowerNowEntity` | string | Entity with solar forecast for current moment |
| `SolarForecastPower30MinutesEntity` | string | Entity with solar forecast for 30 minutes ahead |
| `SolarForecastPower1HourEntity` | string | Entity with solar forecast for 1 hour ahead |

### Battery Manager Configuration

Configure one or more batteries:

```json
"BatteryManager": {
  "Batteries": [
    {
      "Name": "Home Battery",
      "ChargeEntity": "sensor.battery_charge_power",
      "DischargeEntity": "sensor.battery_discharge_power",
      "StateOfChargeEntity": "sensor.battery_soc",
      "MinimumStateOfCharge": 20,
      "MaximumStateOfCharge": 95,
      "Capacity": 10000
    }
  ]
}
```

### Consumer Configuration

Each consumer represents a device or appliance that can be managed by the Energy Manager.

#### Common Consumer Properties

| Property | Type | Description |
|----------|------|-------------|
| `Name` | string | Name of the consumer |
| `ConsumerGroups` | string[] | Groups this consumer belongs to (optional) |
| `PowerUsageEntity` | string | Entity tracking power consumption |
| `SwitchOnLoad` | double | Power threshold for switching on (watts) |
| `SwitchOffLoad` | double | Power threshold for switching off (watts) |
| `MinimumRuntime` | TimeSpan | Minimum time device should run once started |
| `MaximumRuntime` | TimeSpan | Maximum time device can run |
| `MinimumTimeout` | TimeSpan | Minimum time device should stay off |
| `MaximumTimeout` | TimeSpan | Maximum time device can stay off |
| `CriticallyNeededEntity` | string | Entity indicating device is critically needed (optional) |
| `TimeWindows` | TimeWindowConfig[] | Time windows when device can run |
| `LoadTimeFramesToCheckOnStart` | LoadTimeFrames[] | Time frames to check before starting |
| `LoadTimeFramesToCheckOnStop` | LoadTimeFrames[] | Time frames to check before stopping |
| `LoadTimeFrameToCheckOnRebalance` | LoadTimeFrames | Time frame to check when rebalancing |
| `DynamicBalancingMethodBasedLoads` | object[] | Dynamic load configurations based on balancing method |

#### Simple Consumer Example

A basic switch-based consumer:

```json
{
  "Name": "Pool Pump",
  "ConsumerGroups": ["outdoor"],
  "PowerUsageEntity": "sensor.pool_pump_power",
  "SwitchOnLoad": 1500,
  "SwitchOffLoad": 1200,
  "MinimumRuntime": "04:00:00",
  "MaximumRuntime": "08:00:00",
  "TimeWindows": [
    {
      "Start": "08:00:00",
      "End": "18:00:00"
    }
  ],
  "Simple": {
    "SwitchEntity": "switch.pool_pump"
  }
}
```

#### Smart Grid Ready Consumer Example

For heat pumps and other Smart Grid Ready devices:

```json
{
  "Name": "Heat Pump",
  "PowerUsageEntity": "sensor.heat_pump_power",
  "SwitchOnLoad": 2000,
  "SwitchOffLoad": 1500,
  "SmartGridReady": {
    "EnergyDemandEntity": "sensor.heat_pump_energy_demand",
    "SmartGridReadyModeEntity": "select.heat_pump_sgr_mode",
    "ExpectedPowerConsumptionEntity": "sensor.heat_pump_expected_power"
  }
}
```

#### Cooling Consumer Example

For air conditioning and cooling systems:

```json
{
  "Name": "Air Conditioner",
  "PowerUsageEntity": "sensor.ac_power",
  "SwitchOnLoad": 1800,
  "SwitchOffLoad": 1200,
  "Cooling": {
    "ClimateEntity": "climate.living_room_ac",
    "TemperatureSensor": "sensor.living_room_temperature",
    "MinimumTemperature": 22.0,
    "TargetTemperature": 24.0,
    "MaximumTemperature": 26.0
  }
}
```

#### Car Charger Consumer Example

For EV charging:

```json
{
  "Name": "EV Charger",
  "PowerUsageEntity": "sensor.ev_charger_power",
  "SwitchOnLoad": 3000,
  "SwitchOffLoad": 2500,
  "CarCharger": {
    "ChargerEntity": "switch.ev_charger",
    "ChargeStateEntity": "sensor.ev_charge_state",
    "BatterySocEntity": "sensor.ev_battery_soc",
    "MinimumBatterySoc": 20,
    "TargetBatterySoc": 80
  }
}
```

#### Triggered Consumer Example

For devices that need to run based on triggers:

```json
{
  "Name": "Washing Machine",
  "PowerUsageEntity": "sensor.washing_machine_power",
  "SwitchOnLoad": 2000,
  "SwitchOffLoad": 1500,
  "MaximumRuntime": "03:00:00",
  "Triggered": {
    "SwitchEntity": "switch.washing_machine_socket",
    "TriggerEntity": "binary_sensor.washing_machine_ready"
  }
}
```

### Balancing Methods

The Energy Manager supports multiple balancing methods:

- **SolarSurplus**: Only run when there's excess solar production
- **SolarOnly**: Only run during solar production hours
- **MidPoint**: Run when grid import is around mid-point
- **SolarPreferred**: Prefer solar, but allow some grid usage
- **MidPeak**: Run when approaching peak demand
- **NearPeak**: Run when near peak demand threshold
- **MaximizeQuarterPeak**: Optimize for quarter-hour peak reduction

### Load Time Frames

Time frames for checking power availability:

- `Now`: Current power state
- `Last30Seconds`: Average over last 30 seconds
- `LastMinute`: Average over last minute
- `Last2Minutes`: Average over last 2 minutes
- `Last5Minutes`: Average over last 5 minutes
- `SolarForecastNow`: Solar forecast for current time
- `SolarForecastNow50PercentCorrected`: Solar forecast with 50% correction
- `SolarForecast30Minutes`: Solar forecast for 30 minutes ahead
- `SolarForecast1Hour`: Solar forecast for 1 hour ahead

### Time Windows

Configure specific time windows when consumers can run:

```json
"TimeWindows": [
  {
    "Start": "09:00:00",
    "End": "17:00:00",
    "Days": ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"]
  },
  {
    "Start": "08:00:00",
    "End": "20:00:00",
    "Days": ["Saturday", "Sunday"]
  }
]
```

## Complete Example Configuration

```json
{
  "EnergyManager": {
    "Timezone": "Europe/Brussels",
    "Grid": {
      "VoltageEntity": "sensor.grid_voltage",
      "ImportEntity": "sensor.grid_import_power",
      "ExportEntity": "sensor.grid_export_power",
      "PeakImportEntity": "sensor.peak_import_today",
      "CurrentAverageDemandEntity": "sensor.current_average_demand",
      "CurrentSolarPowerEntity": "sensor.solar_power",
      "SolarForecastPowerNowEntity": "sensor.solar_forecast_now",
      "SolarForecastPower30MinutesEntity": "sensor.solar_forecast_30min",
      "SolarForecastPower1HourEntity": "sensor.solar_forecast_1hour"
    },
    "SolarProductionRemainingTodayEntity": "sensor.solar_remaining_today",
    "PhoneToNotify": "mobile_app_iphone",
    "BatteryManager": {
      "Batteries": [
        {
          "Name": "Home Battery",
          "ChargeEntity": "sensor.battery_charge_power",
          "DischargeEntity": "sensor.battery_discharge_power",
          "StateOfChargeEntity": "sensor.battery_soc",
          "MinimumStateOfCharge": 20,
          "MaximumStateOfCharge": 95,
          "Capacity": 10000
        }
      ]
    },
    "Consumers": [
      {
        "Name": "Pool Pump",
        "ConsumerGroups": ["outdoor"],
        "PowerUsageEntity": "sensor.pool_pump_power",
        "SwitchOnLoad": 1500,
        "SwitchOffLoad": 1200,
        "MinimumRuntime": "04:00:00",
        "MaximumRuntime": "08:00:00",
        "LoadTimeFramesToCheckOnStart": ["Last2Minutes"],
        "LoadTimeFramesToCheckOnStop": ["LastMinute"],
        "TimeWindows": [
          {
            "Start": "08:00:00",
            "End": "18:00:00"
          }
        ],
        "Simple": {
          "SwitchEntity": "switch.pool_pump"
        }
      },
      {
        "Name": "Heat Pump",
        "PowerUsageEntity": "sensor.heat_pump_power",
        "SwitchOnLoad": 2000,
        "SwitchOffLoad": 1500,
        "SmartGridReady": {
          "EnergyDemandEntity": "sensor.heat_pump_energy_demand",
          "SmartGridReadyModeEntity": "select.heat_pump_sgr_mode",
          "ExpectedPowerConsumptionEntity": "sensor.heat_pump_expected_power"
        }
      }
    ]
  }
}
```

## Usage

Once configured, the Energy Manager will:

1. Monitor grid conditions and solar production
2. Automatically manage configured consumers based on available power
3. Respect time windows and runtime constraints
4. Balance multiple consumers to optimize energy usage
5. Send notifications when needed (if phone is configured)

The app runs automatically and requires no manual intervention during normal operation.
