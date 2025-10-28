using eLime.NetDaemonApps.Domain.Helper;
using HueApi;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

#pragma warning disable CS8618, CS9264

[assembly: InternalsVisibleTo("eLime.NetDaemonApps.Tests")]
namespace eLime.NetDaemonApps.Domain.HueSeasonalSceneManager;

public class HueSeasonalSceneManager : IDisposable
{
    private string Name { get; set; }
    internal HueSeasonalSceneManagerContext Context { get; private set; }
    internal HueSeasonalSceneManagerState State { get; private set; }
    internal List<HueZone> Zones { get; } = [];

    private LocalHueApi _hueClient;

    private HueSeasonalSceneManager()
    {
    }

    public static async Task<HueSeasonalSceneManager> Create(string name, HueSeasonalSceneManagerConfiguration configuration)
    {
        var hueSeasonalSceneManager = new HueSeasonalSceneManager();
        await hueSeasonalSceneManager.Initialize(name, configuration);
        return hueSeasonalSceneManager;
    }

    private async Task Initialize(string name, HueSeasonalSceneManagerConfiguration configuration)
    {
        Context = configuration.Context;
        Name = name;

        await GetAndSanitizeState();

        if (string.IsNullOrEmpty(State.AppKey))
        {
            Context.Logger.LogInformation("{Name}: No app key found. Attempting to register with Hue Bridge... {Ip}", Name, Context.BridgeIpAddress);
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

        Context.Logger.LogInformation("{Name}: Initialized {ZoneCount} zones.", Name, Zones.Count);
    }

    private async Task RegisterWithBridge()
    {
        try
        {
            Context.Logger.LogInformation("{Name}: Please press the link button on your Hue Bridge within the next 30 seconds...", Name);

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
                        Context.Logger.LogInformation($"{{Name}}: Successfully registered with Hue Bridge! App key: {State.AppKey}", Name);
                        await SaveState();
                        await InitializeHueClient();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Context.Logger.LogDebug($"{{Name}}: Registration attempt failed: {ex.Message}", Name);
                }

                // Wait 2 seconds before trying again
                await Task.Delay(2000);
            }

            Context.Logger.LogError("{Name}: Failed to register with Hue Bridge. Link button was not pressed in time.", Name);
        }
        catch (Exception ex)
        {
            Context.Logger.LogError(ex, "{Name}: Error during Hue Bridge registration", Name);
        }
    }

    private async Task InitializeHueClient()
    {
        try
        {
            _hueClient = new LocalHueApi(Context.BridgeIpAddress, State.AppKey);

            // Test the connection
            var bridge = await _hueClient.GetBridgeAsync();
            Context.Logger.LogInformation($"{{Name}}: Successfully connected to Hue Bridge: {bridge.Data.FirstOrDefault()?.Id.ToString("N") ?? "Unknown"}", Name);
        }
        catch (Exception ex)
        {
            Context.Logger.LogError(ex, "{Name}: Failed to initialize Hue client. App key may be invalid.", Name);
        }
    }

    private Task GetAndSanitizeState()
    {
        var persistedState = Context.FileStorage.Get<HueSeasonalSceneManagerState>("HueSeasonalSceneManager", $"HueSeasonalSceneManager_{Name.MakeHaFriendly()}");
        State = persistedState ?? new HueSeasonalSceneManagerState();

        Context.Logger.LogDebug("{Name}: Retrieved Hue Seasonal Scene Manager state.", Name);
        return Task.CompletedTask;
    }

    private Task SaveState()
    {
        Context.FileStorage.Save("HueSeasonalSceneManager", $"HueSeasonalSceneManager_{Name.MakeHaFriendly()}", State);
        Context.Logger.LogTrace("{Name}: Saved Hue Seasonal Scene Manager state.", Name);
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
        Context.Logger.LogInformation("{Name}: Disposed Hue Seasonal Scene Manager", Name);
    }
}