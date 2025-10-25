# Capacity Calculator

The Capacity Calculator app monitors your electrical system's capacity and provides insights into peak demand management. It connects to smart meters to track real-time power consumption and helps optimize grid connection utilization.

## Features

- **Real-Time Monitoring**: Tracks current power consumption from smart meter
- **Capacity Analysis**: Monitors usage against grid connection capacity
- **Peak Demand Tracking**: Records and analyzes peak consumption periods
- **Historical Data**: Maintains consumption history for analysis
- **Optimization Insights**: Provides data for load balancing and peak shaving

## Configuration

The Capacity Calculator is configured using the `CapacityCalculatorConfig` class in your `appsettings.json`.

### Configuration Structure

```json
{
  "CapacityCalculator": {
    "SmartMeterUrl": "http://192.168.1.50/api/v1/data"
  }
}
```

### Configuration Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `SmartMeterUrl` | string | Yes | URL endpoint for smart meter API |

## Complete Example Configuration

```json
{
  "CapacityCalculator": {
    "SmartMeterUrl": "http://192.168.1.50/api/v1/data"
  }
}
```

## Smart Meter Integration

The app connects to a smart meter's HTTP API endpoint to retrieve real-time power consumption data.

### Supported Smart Meters

The app is designed to work with smart meters that provide HTTP API access. Common compatible devices include:

- **HomeWizard P1 Meter**: Dutch smart meter reader
- **DSMR Reader**: Software for reading Dutch/Belgian smart meters
- **Custom API**: Any smart meter with HTTP JSON API

### API Requirements

The smart meter API should provide:
- Current power consumption (W)
- Voltage levels (V)
- Current draw (A)
- Optional: Historical data, peak values

Example expected JSON response:
```json
{
  "power_w": 2450,
  "voltage_v": 230,
  "current_a": 10.65,
  "peak_power_w": 5200,
  "timestamp": "2024-01-15T14:30:00Z"
}
```

## How It Works

1. **Polling**: Regularly polls the smart meter API (typically every few seconds)
2. **Data Collection**: Retrieves current power consumption and electrical parameters
3. **Analysis**: Calculates:
   - Current capacity utilization percentage
   - Peak demand during monitoring period
   - Average consumption patterns
   - Time-of-use statistics
4. **Storage**: Maintains historical data for trend analysis
5. **Publishing**: Makes data available via MQTT sensors for Home Assistant integration

## MQTT Sensors

The app creates several MQTT sensors:

- `sensor.capacity_calculator_current_power`: Current power consumption (W)
- `sensor.capacity_calculator_capacity_used`: Percentage of grid capacity in use (%)
- `sensor.capacity_calculator_peak_power_today`: Peak power consumption today (W)
- `sensor.capacity_calculator_average_power_1h`: Average power over last hour (W)
- `sensor.capacity_calculator_voltage`: Current grid voltage (V)
- `sensor.capacity_calculator_current`: Current draw (A)

## Usage

Once configured, the Capacity Calculator continuously monitors your electrical system:

### Monitoring Capacity Utilization

Track how much of your grid connection capacity you're using:
- **< 50%**: Normal operation, plenty of headroom
- **50-70%**: Moderate usage, monitor for peak times
- **70-85%**: High usage, consider load management
- **> 85%**: Very high usage, risk of exceeding capacity

### Peak Demand Management

Use the peak tracking to:
- Identify high-consumption periods
- Plan appliance scheduling
- Avoid capacity charges (if applicable)
- Optimize energy consumer coordination

### Integration with Energy Manager

The Capacity Calculator provides essential data for the Energy Manager:

```json
{
  "Grid": {
    "CurrentAverageDemandEntity": "sensor.capacity_calculator_average_power_1h",
    "PeakImportEntity": "sensor.capacity_calculator_peak_power_today"
  }
}
```

This allows the Energy Manager to:
- Prevent exceeding grid capacity
- Balance multiple consumers
- Optimize for peak demand reduction
- Coordinate high-power devices

## Grid Connection Types

Different grid connections have different capacities:

| Connection Type | Capacity | Typical Usage |
|----------------|----------|---------------|
| Single Phase 1x25A | 5.75 kW | Small apartment |
| Single Phase 1x40A | 9.2 kW | Medium home |
| Three Phase 3x25A | 17.25 kW | Large home |
| Three Phase 3x40A | 27.6 kW | Large home with heat pump/EV |
| Three Phase 3x63A | 43.5 kW | Very large home/small business |

Configure your Energy Manager thresholds based on your connection capacity.

## Peak Demand Charges

In some regions, electricity costs are partially based on peak demand:

### Quarter-Hour Peak (Common in Belgium)

- Grid operators measure peak power every 15 minutes
- Monthly capacity charges based on highest 15-min peak
- Reducing peak by 1 kW can save €50-100/year

**Optimization Strategy**:
- Never run multiple high-power devices simultaneously
- Stagger EV charging, heat pump, and appliances
- Use battery storage to shave peaks
- Energy Manager's `MaximizeQuarterPeak` mode helps optimize for this

### Time-of-Use Peaks

- Different rates for different times of day
- Penalties for exceeding capacity during peak hours
- Lower rates during off-peak

**Optimization Strategy**:
- Schedule high-power tasks during off-peak hours
- Use solar/battery during peak rate periods
- Time window configuration in Energy Manager

## Best Practices

1. **Network Reliability**:
   - Use wired connection for smart meter
   - Consider static IP address
   - Monitor API availability

2. **Data Accuracy**:
   - Verify smart meter readings against utility meter
   - Calibrate if needed
   - Account for solar production (net vs. gross consumption)

3. **Update Frequency**:
   - Poll every 1-5 seconds for real-time monitoring
   - Balance between accuracy and network load
   - Match with Energy Manager's response time needs

4. **Historical Data**:
   - Retain at least 24-48 hours of detailed data
   - Keep daily summaries for longer periods
   - Use for pattern analysis and optimization

5. **Alert Thresholds**:
   - Set up notifications for high capacity usage
   - Alert on approaching peak demand limits
   - Warning before exceeding contracted capacity

## Troubleshooting

**No data from smart meter**:
- Verify smart meter URL is correct and accessible
- Check network connectivity
- Ensure API endpoint is correct
- Review smart meter logs for errors

**Inaccurate readings**:
- Compare with utility meter
- Check for solar production offset
- Verify CT clamp orientation (if applicable)
- Calibrate smart meter if supported

**Missing MQTT sensors**:
- Verify MQTT broker connectivity
- Check MQTT entity manager configuration
- Review NetDaemon logs
- Ensure Home Assistant MQTT integration is enabled

**High network load**:
- Reduce polling frequency
- Implement caching
- Use MQTT QoS appropriately
- Check for network issues

## Integration Examples

### Home Assistant Dashboard Card

```yaml
type: vertical-stack
cards:
  - type: gauge
    entity: sensor.capacity_calculator_capacity_used
    name: Grid Capacity
    min: 0
    max: 100
    severity:
      green: 0
      yellow: 70
      red: 85

  - type: sensor
    entity: sensor.capacity_calculator_current_power
    name: Current Consumption
    graph: line

  - type: sensor
    entity: sensor.capacity_calculator_peak_power_today
    name: Peak Today
```

### Automation: High Capacity Warning

```yaml
automation:
  - alias: "Warn High Grid Capacity"
    trigger:
      - platform: numeric_state
        entity_id: sensor.capacity_calculator_capacity_used
        above: 85
    action:
      - service: notify.mobile_app
        data:
          title: "High Grid Usage"
          message: "Grid capacity at {{ states('sensor.capacity_calculator_capacity_used') }}%"
          data:
            priority: high
```

### Script: Capacity Check Before Action

```yaml
script:
  start_high_power_device:
    sequence:
      - condition: numeric_state
        entity_id: sensor.capacity_calculator_capacity_used
        below: 75
      - service: switch.turn_on
        target:
          entity_id: switch.high_power_device
      - wait_template: "{{ states('sensor.capacity_calculator_capacity_used') | float < 90 }}"
        timeout: "00:05:00"
```

## Advanced Features

### Load Prediction

Use historical data to predict future loads:
- Time-of-day patterns
- Day-of-week variations
- Seasonal trends
- Weather correlations

### Peak Shaving Strategy

Coordinate with Energy Manager:
1. Monitor approaching peak
2. Shed non-critical loads
3. Deploy battery storage
4. Defer deferrable consumers
5. Resume after peak passes

### Capacity Planning

Use data for infrastructure decisions:
- Is grid connection sufficient?
- When to upgrade capacity?
- ROI on battery storage
- Sizing of backup generator

## Smart Meter Selection

When choosing a smart meter for use with this app:

**Requirements**:
- HTTP API access
- JSON or XML data format
- Real-time updates (< 5 second lag)
- Power consumption in watts
- Voltage and current readings

**Optional Features**:
- Historical data storage
- Multiple phase support
- Import/export differentiation
- Power quality metrics (THD, power factor)

**Recommended Models**:
- HomeWizard P1 Meter (Netherlands/Belgium)
- DSMR Reader with compatible meter
- Shelly EM with API enabled
- Custom ESPHome-based solutions

## Privacy and Security

- Smart meter data can reveal detailed home activity patterns
- Secure the API endpoint (authentication, HTTPS)
- Limit network access to smart meter
- Consider data retention policies
- Review who has access to consumption data

## Energy Insights

Data provided by Capacity Calculator enables:
- **Cost Optimization**: Reduce peak demand charges
- **Load Balancing**: Distribute consumption evenly
- **Solar Optimization**: Maximize self-consumption
- **Battery Sizing**: Determine optimal capacity
- **EV Charging**: Smart charging schedules
- **Appliance Scheduling**: Run during low-demand periods
- **Grid Independence**: Track towards off-grid capability
