# Hue Seasonal Scene Manager

This NetDaemon app connects to a Philips Hue Bridge and manages seasonal scenes using the HueApi 3.0 library.

## Features

- **Automatic App Key Registration**: On first run, the app will attempt to register with your Hue Bridge
- **Persistent Storage**: The app key is saved to file storage and persists across restarts
- **Seasonal Scene Management**: Manages Hue scenes based on seasons (placeholder for future functionality)
- **Light Control**: Includes methods to get lights and control them (turn on/off)

## Configuration

Add the following configuration to your `appsettings.json` or configuration file:

```json
{
  "HueSeasonalSceneManager": {
    "BridgeIpAddress": "192.168.1.100"
  }
}
```

Replace `192.168.1.100` with your Hue Bridge's IP address.

## First Time Setup

1. Configure the Bridge IP address in your appsettings
2. Start the NetDaemon app
3. When prompted in the logs, **press the link button on your Hue Bridge** within 30 seconds
4. The app will automatically register and save the app key
5. On subsequent starts, the app will use the saved app key

## State Persistence

The app key is stored in the file storage under:
- **App Name**: `HueSeasonalSceneManager`
- **ID**: `HueSeasonalSceneManager`
- **State Object**: `HueSeasonalSceneManagerState`

The state includes:
- `AppKey`: The authentication key for the Hue Bridge
- `AppKeyCreatedAt`: Timestamp when the key was created

## Usage Examples

### Get All Lights

```csharp
var lights = await _hueSeasonalSceneManager.GetLightsAsync();
if (lights != null)
{
    foreach (var light in lights)
    {
        Console.WriteLine($"Light: {light.Metadata?.Name} (ID: {light.Id})");
    }
}
```

### Turn On a Light

```csharp
var lightId = Guid.Parse("your-light-id");
await _hueSeasonalSceneManager.TurnOnLightAsync(lightId);
```

### Turn Off a Light

```csharp
var lightId = Guid.Parse("your-light-id");
await _hueSeasonalSceneManager.TurnOffLightAsync(lightId);
```

## Architecture

The app follows the standard pattern used in eLime.NetDaemonApps:

- **Config Project**: `HueSeasonalSceneManagerConfig.cs` - Configuration model
- **Domain Project**: 
  - `HueSeasonalSceneManager.cs` - Main business logic
  - `HueSeasonalSceneManagerState.cs` - State model for persistence
  - `HueSeasonalSceneManagerContext.cs` - Context with dependencies
- **Apps Project**: `HueSeasonalSceneManager.cs` - NetDaemon app wrapper

## Dependencies

- **HueApi 3.0.0**: Philips Hue API client library
- **NetDaemon.Extensions.Scheduler**: For scheduling operations
- **IFileStorage**: For state persistence

## Troubleshooting

### App Key Registration Fails

- Ensure the Hue Bridge IP address is correct
- Make sure you press the link button within 30 seconds of the app starting
- Check that the Hue Bridge is on the same network

### Connection Fails After Registration

- The app will automatically invalidate and clear a bad app key
- Simply restart the app and press the link button again to re-register
