using eLime.NetDaemonApps.Config;
using eLime.NetDaemonApps.Domain.Helper;
using HueApi.Models.Requests.SmartScene;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.Scheduler;

#pragma warning disable CS8618, CS9264

namespace eLime.NetDaemonApps.Domain.HueSeasonalSceneManager;

public class HueZone : IDisposable
{
    internal HueZoneContext Context { get; private set; }
    internal HueZoneHomeAssistantEntities HomeAssistant { get; private set; }
    internal HueZoneState State { get; private set; }
    public string Zone { get; }
    public Guid AllDaySceneId { get; }
    public SceneTimeSlotConfig FallbackScenes { get; }

    private IDisposable? _dailyScheduleSubscription;

    private HueZone(string zone, Guid allDaySceneId, SceneTimeSlotConfig fallbackScenes)
    {
        Zone = zone;
        AllDaySceneId = allDaySceneId;
        FallbackScenes = fallbackScenes;
    }

    public static async Task<HueZone> Create(HueZoneContext context, HueZoneConfiguration configuration)
    {
        var zone = new HueZone(configuration.Zone, configuration.AllDaySceneId, configuration.FallbackScenes);
        await zone.Initialize(context, configuration);
        return zone;
    }

    private async Task Initialize(HueZoneContext context, HueZoneConfiguration configuration)
    {
        Context = context;
        HomeAssistant = new HueZoneHomeAssistantEntities(configuration);

        GetAndSanitizeState();

        var minute = Random.Shared.Next(0, 59);
        var startTime = new TimeOnly(00, minute);
        var startFrom = startTime.GetUtcDateTimeFromLocalTimeOnly(Context.Scheduler.Now.DateTime, "Europe/Brussels").AddDays(1);

        _dailyScheduleSubscription = Context.Scheduler.RunEvery(TimeSpan.FromDays(1), startFrom, OnDailyCheck);

        Context.Logger.LogInformation("{Zone}: Zone initialized. Daily check scheduled at {StartTime}.", Zone, $"{startTime:t}");

        // Run initial check
        OnDailyCheck();

        await SaveState();
    }

    private async void OnDailyCheck()
    {
        try
        {
            Context.Logger.LogTrace("{Zone}: Running daily seasonal scene check...", Zone);
            await ApplyActiveScenes();
        }
        catch (Exception e)
        {
            Context.Logger.LogError(e, "{Zone}: Error during daily seasonal scene check", Zone);
        }
    }

    public async Task ApplyActiveScenes()
    {
        try
        {
            var activeSeasonalScene = HomeAssistant.SeasonalScenes.FirstOrDefault(s => s.OperatingModeSensor.IsOn());
            var seasonalSceneToActivate = activeSeasonalScene != null
                ? Context.Configuration.SeasonalScenes.Single(x => x.OperatingModeSensor.EntityId == activeSeasonalScene.OperatingModeSensor.EntityId)
                    : null;

            if (State.CurrentlyAppliedFestivity == seasonalSceneToActivate?.Festivity)
                return;

            if (seasonalSceneToActivate != null)
            {
                Context.Logger.LogInformation("{Zone}: Applying seasonal scenes for {Festivity}...", Zone, seasonalSceneToActivate.Festivity);
                var succeeded = await ApplyScenesFromConfig(seasonalSceneToActivate.Festivity);
                if (succeeded)
                    State.CurrentlyAppliedFestivity = seasonalSceneToActivate.Festivity;
            }
            else
            {
                Context.Logger.LogInformation("{Zone}: No active seasonal scene, applying fallback scenes...", Zone);
                var succeeded = await ApplyFallbackScenes();

                if (succeeded)
                    State.CurrentlyAppliedFestivity = null;
            }

            await SaveState();
        }
        catch (Exception ex)
        {
            Context.Logger.LogError(ex, "{Zone}: Error applying scenes", Zone);
        }
    }

    private Task<bool> ApplyScenesFromConfig(string festivity)
    {
        var sceneConfig = Context.Configuration.SeasonalScenes.Single(x => x.Festivity == festivity);
        return UpdateSmartScene(sceneConfig.Scenes);
    }

    private Task<bool> ApplyFallbackScenes()
    {
        return UpdateSmartScene(FallbackScenes);
    }

    private async Task<bool> UpdateSmartScene(SceneTimeSlotConfig sceneConfig)
    {
        try
        {
            var weekTimeSlots = await Context.HueClient.SmartScene.GetByIdAsync(AllDaySceneId);
            var updateSmartScene = new UpdateSmartScene
            {
                WeekTimeslots = weekTimeSlots.Data.First().WeekTimeslots
            };

            // Update each time slot with the corresponding scene
            // Map Timeslot1-6 to the Hue smart scene timeslots
            var timeslots = updateSmartScene.WeekTimeslots.First().Timeslots;

            if (!string.IsNullOrEmpty(sceneConfig.Timeslot1))
                timeslots[0].Target.Rid = Guid.Parse(sceneConfig.Timeslot1);

            if (!string.IsNullOrEmpty(sceneConfig.Timeslot2))
                timeslots[1].Target.Rid = Guid.Parse(sceneConfig.Timeslot2);

            if (!string.IsNullOrEmpty(sceneConfig.Timeslot3))
                timeslots[2].Target.Rid = Guid.Parse(sceneConfig.Timeslot3);

            if (!string.IsNullOrEmpty(sceneConfig.Timeslot4))
                timeslots[3].Target.Rid = Guid.Parse(sceneConfig.Timeslot4);

            if (!string.IsNullOrEmpty(sceneConfig.Timeslot5))
                timeslots[4].Target.Rid = Guid.Parse(sceneConfig.Timeslot5);

            if (!string.IsNullOrEmpty(sceneConfig.Timeslot6))
                timeslots[5].Target.Rid = Guid.Parse(sceneConfig.Timeslot6);

            foreach (var timeslot in timeslots)
            {
                if (timeslot.StartTime.Kind == "sunset")
                    timeslot.StartTime.Time = null;
            }
            var response = await Context.HueClient.SmartScene.UpdateAsync(AllDaySceneId, updateSmartScene);

            if (response.HasErrors)
            {
                Context.Logger.LogError("{Zone}: Failed to update smart scene {SceneId}. Errors: {Errors}", Zone, AllDaySceneId, response.Errors);
                return false;
            }
            Context.Logger.LogTrace("{Zone}: Successfully updated smart scene {SceneId}", Zone, AllDaySceneId);
            return true;
        }
        catch (Exception ex)
        {
            Context.Logger.LogError(ex, "{Zone}: Failed to update smart scene {SceneId}", Zone, AllDaySceneId);
        }

        return false;
    }

    private void GetAndSanitizeState()
    {
        var persistedState = Context.FileStorage.Get<HueZoneState>("HueSeasonalSceneManager", $"Zone_{Zone.MakeHaFriendly()}");
        State = persistedState ?? new HueZoneState
        {
            Zone = Zone
        };
    }

    private Task SaveState()
    {
        Context.FileStorage.Save("HueSeasonalSceneManager", $"Zone_{Zone.MakeHaFriendly()}", State);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _dailyScheduleSubscription?.Dispose();
        HomeAssistant.Dispose();
        Context.Logger.LogInformation("{Zone}: Disposed zone", Zone);
    }
}
