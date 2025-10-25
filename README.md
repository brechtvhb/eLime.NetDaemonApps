# eLime.NetDaemonApps

A collection of intelligent home automation apps for Home Assistant using NetDaemon V3. These apps provide advanced automation for energy management, lighting, climate control, irrigation, and more.

## Apps Overview

### ?? [Energy Manager](eLime.NetDaemonApps/apps/EnergyManager/README.md)

Intelligently manages energy consumers based on solar production, grid capacity, and battery state. Optimizes device scheduling to maximize solar self-consumption and minimize grid import.

**Key Features:**
- Smart Grid Management with solar forecasting
- Multiple consumer types (Simple, Smart Grid Ready, Cooling, Car Charger)
- Battery management and load balancing
- Dynamic power distribution with multiple balancing methods
- Time window scheduling and consumer groups

**Perfect for:** Homes with solar panels, battery storage, heat pumps, EV chargers, and multiple high-power appliances.

---

### ??? [Smart Heat Pump](eLime.NetDaemonApps/apps/SmartHeatPump/README.md)

Advanced heat pump management with Smart Grid Ready functionality. Automatically calculates energy demands for room heating and hot water, manages temperature setpoints, and optimizes operation for energy efficiency.

**Key Features:**
- Smart Grid Ready (SGR) mode control
- Energy demand management for heating and hot water
- Shower/bath request handling with priority heating
- COP (Coefficient of Performance) monitoring
- ISG (Internet Service Gateway) integration

**Perfect for:** Stiebel Eltron or other SGR-compatible heat pumps with ISG integration.

---

### ?? [FlexiScreens](eLime.NetDaemonApps/apps/FlexiScreens/README.md)

Intelligent automation for window screens, blinds, and shutters. Automatically manages screens based on sun position, weather conditions, temperature, and sleep schedules while respecting manual overrides.

**Key Features:**
- Sun protection based on position and orientation
- Storm protection (wind, rain, forecasts)
- Temperature-based control to prevent overheating
- Sleep mode integration
- Manual override with configurable timeout

**Perfect for:** Homes with motorized screens/blinds that need automated sun, storm, and temperature protection.

---

### ?? [Smart Irrigation](eLime.NetDaemonApps/apps/SmartIrrigation/README.md)

Comprehensive water management system for gardens, lawns, and plants. Supports multiple irrigation types with weather integration and rainwater management.

**Key Features:**
- Classic irrigation (soil moisture-based)
- Container irrigation (volume-based with overflow protection)
- Anti-frost misting for plant protection
- Weather forecast integration
- Rainwater tank management with pump protection
- Multi-zone support with seasonal operation

**Perfect for:** Gardens with rainwater collection, soil moisture sensors, and automated valve systems.

---

### ??? [Smart Ventilation](eLime.NetDaemonApps/apps/SmartVentilation/README.md)

Advanced HVAC/ventilation management using a multi-guard approach. Optimizes ventilation based on air quality, humidity, temperature, and occupancy for comfort, health, and energy efficiency.

**Key Features:**
- Indoor air quality monitoring (CO2 levels)
- Bathroom humidity control
- Mold prevention guard
- Dry air prevention
- Energy saving during away/sleep periods
- State ping-pong prevention

**Perfect for:** Heat recovery ventilation systems, whole-house ventilation with multiple sensors.

---

### ?? [FlexiLights](eLime.NetDaemonApps/apps/FlexiLights/README.md)

Context-aware lighting automation with advanced scene management. Goes beyond simple on/off by supporting multiple scenes per room, complex conditions, and intelligent transitions.

**Key Features:**
- Multiple FlexiScenes per room with conditions
- Motion sensor integration with timeouts
- Advanced switch control (single, double, triple, long press)
- Illuminance-based automation
- Scene chaining and auto-transitions
- Presence simulation support

**Perfect for:** Complex lighting setups with multiple scenes, motion sensors, and smart switches.

---

### ?? [Lights Randomizer](eLime.NetDaemonApps/apps/LightRandomizer/README.md)

Creates realistic lighting patterns to simulate presence when away from home. Enhances security by making your home appear occupied with randomized zone activation.

**Key Features:**
- Random zone selection with configurable count
- Scene-based activation for realistic patterns
- Guard control via away mode sensor
- Time-based randomization

**Perfect for:** Vacation/away security, deterring potential intruders with realistic lighting patterns.

---

### ?? [Solar Backup](eLime.NetDaemonApps/apps/SolarBackup/README.md)

Automates server and storage backups during peak solar production. Schedules resource-intensive backup operations when excess solar power is available to minimize grid consumption.

**Key Features:**
- Synology NAS integration (Wake-on-LAN, shutdown)
- Proxmox VE backup management
- Proxmox Backup Server integration (verify, prune)
- Intelligent scheduling with solar preference
- Critical backup fallback for cloudy periods

**Perfect for:** Home labs with Proxmox, Synology NAS, solar panels, and desire to minimize backup energy costs.

---

### ? [Capacity Calculator](eLime.NetDaemonApps/apps/CapacityCalculator/README.md)

Monitors electrical system capacity and provides insights for peak demand management. Tracks real-time power consumption from smart meters to optimize grid connection utilization.

**Key Features:**
- Real-time smart meter monitoring
- Grid capacity utilization tracking
- Peak demand analysis
- Historical data for pattern analysis
- Integration with Energy Manager

**Perfect for:** Homes with smart meters, peak demand charges, or concerns about exceeding grid connection capacity.

---

## Getting Started

### Prerequisites

- Home Assistant with NetDaemon V3 add-on or standalone installation
- .NET 9 SDK (for development)
- MQTT broker (for sensor integration)

### Installation

1. Clone this repository
2. Configure your apps in `appsettings.json` (see individual app READMEs for configuration details)
3. Deploy to your NetDaemon instance
4. Apps will start automatically and create necessary MQTT entities

### Configuration

Each app has its own configuration section in `appsettings.json`. Refer to the individual app README files (linked above) for detailed configuration examples and property descriptions.

Example structure:
```json
{
  "EnergyManager": { /* ... */ },
  "SmartHeatPump": { /* ... */ },
  "FlexiScreens": { /* ... */ },
  "SmartIrrigation": { /* ... */ },
  "SmartVentilation": { /* ... */ },
  "FlexiLights": { /* ... */ },
  "LightsRandomizer": { /* ... */ },
  "SolarBackup": { /* ... */ },
  "CapacityCalculator": { /* ... */ }
}
```

## App Integration

Many apps work together seamlessly:

- **Energy Manager** ?? **Smart Heat Pump**: Optimizes heat pump operation based on solar availability
- **Energy Manager** ?? **Solar Backup**: Schedules backups during excess solar production
- **Energy Manager** ?? **Capacity Calculator**: Prevents exceeding grid capacity
- **FlexiScreens** ?? **Smart Ventilation**: Coordinates window management with ventilation
- **FlexiLights** ?? **Lights Randomizer**: Can disable automation during presence simulation

## Documentation

Each app has comprehensive documentation including:
- Feature overview
- Complete configuration reference
- Example configurations
- Usage tips and best practices
- Troubleshooting guides
- Integration examples

Click on any app name above to view its detailed README.

## Contributing

Contributions are welcome! Please feel free to submit issues, feature requests, or pull requests.

## License

[License information here]

## Support

For issues, questions, or feature requests, please use the GitHub issue tracker.
