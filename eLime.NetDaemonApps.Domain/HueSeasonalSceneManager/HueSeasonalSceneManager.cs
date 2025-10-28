using HueApi;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

#pragma warning disable CS8618, CS9264

[assembly: InternalsVisibleTo("eLime.NetDaemonApps.Tests")]
namespace eLime.NetDaemonApps.Domain.HueSeasonalSceneManager;

public class HueSeasonalSceneManager : IDisposable
{
    internal HueSeasonalSceneManagerContext Context { get; private set; }
    internal HueSeasonalSceneManagerState State { get; private set; }
    internal List<HueZone> Zones { get; } = [];

    private LocalHueApi _hueClient;

    private HueSeasonalSceneManager()
    {
    }

    public static async Task<HueSeasonalSceneManager> Create(HueSeasonalSceneManagerConfiguration configuration)
    {
        var hueSeasonalSceneManager = new HueSeasonalSceneManager();
        await hueSeasonalSceneManager.Initialize(configuration);
        return hueSeasonalSceneManager;
    }

    private async Task Initialize(HueSeasonalSceneManagerConfiguration configuration)
    {
        Context = configuration.Context;

        await GetAndSanitizeState();

        if (string.IsNullOrEmpty(State.AppKey))
        {
            Context.Logger.LogInformation("No app key found. Attempting to register with Hue Bridge...");
            await RegisterWithBridge();
        }
        else
        {
            Context.Logger.LogInformation("App key found. Initializing Hue client...");
            await InitializeHueClient();
        }

        foreach (var zoneConfig in configuration.Zones)
        {
            var zoneContext = new HueZoneContext(Context.Logger, Context.Scheduler, Context.FileStorage, _hueClient, zoneConfig);
            var zone = await HueZone.Create(zoneContext, zoneConfig);
            Zones.Add(zone);
        }

        Context.Logger.LogInformation("Initialized {ZoneCount} zones.", Zones.Count);
    }

    private async Task RegisterWithBridge()
    {
        try
        {
            Context.Logger.LogInformation("Please press the link button on your Hue Bridge within the next 30 seconds...");

            var appName = "eLime.NetDaemonApps";
            var deviceName = Environment.MachineName;

            // Try to register for 30 seconds
            var endTime = Context.Scheduler.Now.AddSeconds(30);

            while (Context.Scheduler.Now < endTime)
            {
                try
                {
                    var registerResult = await LocalHueApi.RegisterAsync(Context.BridgeIpAddress, appName, deviceName, true);

                    if (!string.IsNullOrEmpty(registerResult?.Username))
                    {
                        State.AppKey = registerResult.Username;
                        Context.Logger.LogInformation($"Successfully registered with Hue Bridge! App key: {State.AppKey}");
                        await SaveState();
                        await InitializeHueClient();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Context.Logger.LogDebug($"Registration attempt failed: {ex.Message}");
                }

                // Wait 2 seconds before trying again
                await Task.Delay(2000);
            }

            Context.Logger.LogError("Failed to register with Hue Bridge. Link button was not pressed in time.");
        }
        catch (Exception ex)
        {
            Context.Logger.LogError(ex, "Error during Hue Bridge registration");
        }
    }

    private async Task InitializeHueClient()
    {
        try
        {
            _hueClient = new LocalHueApi(Context.BridgeIpAddress, State.AppKey);

            // Test the connection
            var bridge = await _hueClient.GetBridgeAsync();
            Context.Logger.LogInformation($"Successfully connected to Hue Bridge: {bridge.Data.FirstOrDefault()?.Id.ToString("N") ?? "Unknown"}");
        }
        catch (Exception ex)
        {
            Context.Logger.LogError(ex, "Failed to initialize Hue client. App key may be invalid.");
            State.AppKey = null;
            await SaveState();
        }
    }

    private Task GetAndSanitizeState()
    {
        var persistedState = Context.FileStorage.Get<HueSeasonalSceneManagerState>("HueSeasonalSceneManager", "HueSeasonalSceneManager");
        State = persistedState ?? new HueSeasonalSceneManagerState();

        Context.Logger.LogDebug("Retrieved Hue Seasonal Scene Manager state.");
        return Task.CompletedTask;
    }

    private Task SaveState()
    {
        Context.FileStorage.Save("HueSeasonalSceneManager", "HueSeasonalSceneManager", State);
        Context.Logger.LogDebug("Saved Hue Seasonal Scene Manager state.");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        foreach (var zone in Zones)
        {
            zone.Dispose();
        }

        // LocalHueApi in HueApi 3.0 doesn't implement IDisposable
        _hueClient = null;
        Context.Logger.LogInformation("Disposed Hue Seasonal Scene Manager");
    }
}
