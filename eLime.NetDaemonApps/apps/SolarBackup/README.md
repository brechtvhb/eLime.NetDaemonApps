# Solar Backup

The Solar Backup app automates backup operations for servers and storage systems when excess solar power is available. It intelligently schedules resource-intensive backup tasks during peak solar production to minimize grid consumption and maximize use of renewable energy.

## Features

- **Solar-Powered Backups**: Schedule backups during solar production hours
- **Synology NAS Integration**: Wake-on-LAN and shutdown automation for Synology NAS
- **Proxmox VE Integration**: Manage Proxmox Virtual Environment backups and storage
- **Proxmox Backup Server Integration**: Verify and prune backups on PBS
- **Intelligent Scheduling**: Regular backups with solar preference, critical backups when needed
- **Resource Management**: Prevents backup conflicts and manages storage efficiently
- **Energy Optimization**: Minimizes grid power usage for backup operations

## Configuration

The Solar Backup app is configured using the `SolarBackupConfig` class in your `appsettings.json`.

### Configuration Structure

```json
{
  "SolarBackup": {
    "BackupInterval": "1.00:00:00",
    "CriticalBackupInterval": "7.00:00:00",
    "Synology": {
      "Mac": "00:11:32:XX:XX:XX",
      "BroadcastAddress": "192.168.1.255",
      "ShutDownButton": "button.synology_shutdown"
    },
    "Pve": {
      "Url": "https://pve.local:8006",
      "Token": "PVEAPIToken=user@pam!token=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
      "Cluster": "pve",
      "StorageId": "local",
      "StorageName": "local"
    },
    "Pbs": {
      "Url": "https://pbs.local:8007",
      "Token": "PBSAPIToken=user@pbs!token=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
      "DataStore": "backups",
      "VerifyJobId": "verify-all",
      "PruneJobId": "prune-old"
    }
  }
}
```

### Configuration Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `BackupInterval` | TimeSpan | Yes | Regular backup interval (format: "days.hours:minutes:seconds") |
| `CriticalBackupInterval` | TimeSpan | Yes | Maximum time without backup before forcing critical backup |
| `Synology` | object | No | Synology NAS configuration |
| `Pve` | object | No | Proxmox VE configuration |
| `Pbs` | object | No | Proxmox Backup Server configuration |

### Synology Configuration

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Mac` | string | Yes | MAC address for Wake-on-LAN |
| `BroadcastAddress` | string | Yes | Network broadcast address for WoL packet |
| `ShutDownButton` | string | Yes | Home Assistant button entity for Synology shutdown |

**Setup Requirements**:
- Enable Wake-on-LAN in Synology DSM (Control Panel ? Hardware & Power ? General)
- Configure Synology integration in Home Assistant
- Ensure network allows WoL packets

### Proxmox VE Configuration

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Url` | string | Yes | Proxmox VE URL (e.g., https://pve.local:8006) |
| `Token` | string | Yes | API token for authentication (format: "PVEAPIToken=user@realm!tokenid=uuid") |
| `Cluster` | string | Yes | Cluster name (usually "pve" for single-node setups) |
| `StorageId` | string | Yes | Storage ID where backups are stored |
| `StorageName` | string | Yes | Storage name (display name) |

**Token Creation**:
1. Log into Proxmox VE web interface
2. Navigate to Datacenter ? Permissions ? API Tokens
3. Click "Add" and create token with appropriate permissions
4. Required permissions: Datastore.Allocate, VM.Backup, VM.Audit

### Proxmox Backup Server Configuration

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Url` | string | Yes | PBS URL (e.g., https://pbs.local:8007) |
| `Token` | string | Yes | API token (format: "PBSAPIToken=user@realm!tokenid=uuid") |
| `DataStore` | string | Yes | Datastore name where backups are stored |
| `VerifyJobId` | string | No | Scheduled verify job ID to monitor |
| `PruneJobId` | string | No | Scheduled prune job ID to monitor |

**Token Creation**:
1. Log into PBS web interface
2. Navigate to Configuration ? Access Control ? API Tokens
3. Create token with Datastore.Audit, Datastore.Verify, Datastore.Prune permissions

## Complete Example Configuration

```json
{
  "SolarBackup": {
    "BackupInterval": "1.00:00:00",
    "CriticalBackupInterval": "7.00:00:00",
    "Synology": {
      "Mac": "00:11:32:AB:CD:EF",
      "BroadcastAddress": "192.168.1.255",
      "ShutDownButton": "button.synology_nas_shutdown"
    },
    "Pve": {
      "Url": "https://192.168.1.100:8006",
      "Token": "PVEAPIToken=backup@pam!solar-backup=12345678-1234-1234-1234-123456789012",
      "Cluster": "homelab",
      "StorageId": "local",
      "StorageName": "local"
    },
    "Pbs": {
      "Url": "https://192.168.1.101:8007",
      "Token": "PBSAPIToken=backup@pbs!solar-backup=87654321-4321-4321-4321-210987654321",
      "DataStore": "backup-storage",
      "VerifyJobId": "verify-daily",
      "PruneJobId": "prune-weekly"
    }
  }
}
```

## How It Works

### Backup Scheduling

1. **Solar Monitoring**: Continuously monitors solar production
2. **Backup Due Check**: Determines if backup is needed based on intervals
3. **Solar Availability**: Waits for sufficient solar power (if not critical)
4. **Execution**:
   - Wake Synology NAS (if configured)
   - Wait for system to be ready
   - Trigger Proxmox backup jobs
   - Verify backups on PBS
   - Prune old backups
   - Shutdown Synology (if configured)

### Backup Types

**Regular Backup**:
- Triggered when `BackupInterval` has elapsed since last backup
- Waits for optimal solar conditions
- Example: Daily backups during peak solar (typically 10:00-14:00)

**Critical Backup**:
- Forced when `CriticalBackupInterval` has elapsed
- Runs regardless of solar availability
- Ensures backups happen even during extended cloudy periods
- Example: Weekly maximum ensures no more than 7 days without backup

### Workflow

```
1. Check if backup is due (BackupInterval elapsed)
   ?? No ? Wait
   ?? Yes ? Continue

2. Check if critical (CriticalBackupInterval elapsed)
   ?? Yes ? Force backup immediately
   ?? No ? Wait for solar conditions

3. Solar conditions met OR critical backup
   ?? Wake Synology NAS (if configured)
   ?? Wait for NAS to be ready
   ?? Trigger Proxmox VE backups
   ?? Wait for completion
   ?? Verify backups on PBS
   ?? Prune old backups on PBS
   ?? Shutdown Synology NAS (if configured)
   ?? Update last backup timestamp

4. Monitor backup jobs and handle failures
```

## Solar Integration

The app integrates with solar monitoring to determine optimal backup times:

- **Peak Solar Hours**: Typically 10:00-15:00 depending on location
- **Available Power**: Requires sufficient excess solar power (typically 300W+)
- **Grid Independence**: Minimizes grid import during backups
- **Battery Consideration**: Can use battery storage for evening backups if configured

### Energy Manager Integration

When used with Energy Manager:
- Backup operations can be registered as energy consumers
- Coordinated with other high-power devices
- Optimized scheduling based on solar forecast
- Battery state of charge consideration

## Usage

Once configured, the Solar Backup app operates automatically:

1. **Monitoring**: Continuously checks if backup is due
2. **Waiting**: Waits for optimal solar conditions (unless critical)
3. **Execution**: Performs backup operations when conditions are met
4. **Verification**: Verifies backup integrity
5. **Cleanup**: Prunes old backups according to retention policy
6. **Shutdown**: Powers down backup target if configured

### Manual Triggers

You can create Home Assistant automations or scripts to manually trigger backups:

```yaml
script:
  trigger_backup:
    alias: "Trigger Manual Backup"
    sequence:
      - service: automation.trigger
        target:
          entity_id: automation.solar_backup
        data:
          skip_condition: true
```

## Monitoring

The app may create MQTT sensors for monitoring:
- Last backup timestamp
- Next expected backup time
- Backup status (idle, running, completed, failed)
- Last backup duration
- Storage usage

## Best Practices

1. **Backup Intervals**:
   - `BackupInterval`: 1-2 days for active systems
   - `CriticalBackupInterval`: 7-14 days maximum
   - Balance between freshness and storage/power usage

2. **Storage Management**:
   - Configure appropriate retention policies in Proxmox/PBS
   - Monitor storage capacity
   - Regular prune jobs to free space

3. **Network Configuration**:
   - Ensure WoL packets can reach NAS
   - Verify firewall allows Proxmox API access
   - Use static IPs for backup targets

4. **Security**:
   - Use API tokens (not passwords)
   - Limit token permissions to minimum required
   - Enable HTTPS for all API connections
   - Consider network segmentation for backup traffic

5. **Testing**:
   - Test NAS wake/shutdown cycle
   - Verify backup restoration periodically
   - Monitor backup completion notifications
   - Test critical backup trigger

6. **Resource Planning**:
   - Backups can take 30 minutes to 2+ hours
   - Require 200-500W during backup operations
   - Plan for sufficient solar/battery capacity

## Troubleshooting

**Synology won't wake**:
- Verify MAC address is correct
- Check WoL is enabled in Synology
- Ensure network allows broadcast packets
- Try WoL from another device to test

**Proxmox API errors**:
- Verify token has correct permissions
- Check SSL certificate validity
- Ensure Proxmox is accessible from NetDaemon host
- Review Proxmox logs for authentication issues

**Backups not triggering**:
- Check solar conditions are being met
- Verify BackupInterval configuration
- Review NetDaemon logs for errors
- Test with forced critical backup

**Backup failures**:
- Check available storage space
- Verify VM/container state
- Review Proxmox task logs
- Ensure backup target is accessible

**PBS verification fails**:
- Check datastore integrity
- Verify network connectivity
- Review PBS logs
- Ensure sufficient resources on PBS

## Storage Requirements

Typical backup sizes and retention suggestions:

- **Small VMs** (< 50GB): Keep 7-14 days, hourly backups possible
- **Medium VMs** (50-200GB): Keep 7 days, daily backups
- **Large VMs** (> 200GB): Keep 3-5 days, daily backups
- **Containers** (< 10GB): Keep 14-30 days, multiple daily backups

Calculate storage: `VM_Size × Retention_Days × Number_of_VMs × 1.2 (overhead)`

## Performance Optimization

- **Parallel Backups**: Proxmox can backup multiple VMs simultaneously
- **Compression**: Enable compression in backup jobs to save space
- **Incremental**: Use PBS incremental backups to reduce transfer time
- **Scheduling**: Stagger backup start times for different VM groups
- **Bandwidth Limiting**: Limit backup bandwidth if affecting other services

## Integration with Energy Manager

Example Energy Manager consumer configuration for Solar Backup:

```json
{
  "Name": "Backup System",
  "ConsumerGroups": ["maintenance"],
  "PowerUsageEntity": "sensor.backup_system_power",
  "SwitchOnLoad": 300,
  "SwitchOffLoad": 200,
  "MinimumRuntime": "01:00:00",
  "MaximumRuntime": "03:00:00",
  "TimeWindows": [
    {
      "Start": "09:00:00",
      "End": "16:00:00"
    }
  ],
  "Triggered": {
    "SwitchEntity": "input_boolean.backup_requested",
    "TriggerEntity": "binary_sensor.solar_excess_available"
  }
}
```
