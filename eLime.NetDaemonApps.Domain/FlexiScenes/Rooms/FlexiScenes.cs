using eLime.NetDaemonApps.Domain.Helper;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.FlexiScenes.Rooms;

public class FlexiScenes
{
    internal List<FlexiSceneChange> Changes { get; set; } = [];
    public TimeSpan SaveChangesFor = TimeSpan.FromDays(8);

    private string? CurrentFlexiScene { get; set; }

    //used when cycling through scenes
    private string? InitialFlexiScene { get; set; }

    private readonly CircularReadOnlyList<FlexiScene> _flexiScenes = [];

    internal FlexiScenes(IEnumerable<FlexiScene> flexiScenes)
    {
        _flexiScenes.AddRange(flexiScenes);
    }

    public IReadOnlyList<FlexiScene> All => _flexiScenes.AsReadOnly();
    internal FlexiScene? GetSceneThatShouldActivate(IReadOnlyCollection<Entity> flexiSceneSensors) => All.FirstOrDefault(x => x.CanActivate(flexiSceneSensors));
    internal FlexiScene? GetSceneThatShouldActivateFullyAutomated(IReadOnlyCollection<Entity> flexiSceneSensors) => All.FirstOrDefault(x => x.CanActivateFullyAutomated(flexiSceneSensors));
    public FlexiScene? Current => All.SingleOrDefault(x => x.Name == CurrentFlexiScene);
    public FlexiScene? Initial => All.SingleOrDefault(x => x.Name == InitialFlexiScene);

    public FlexiScene? GetByName(string name) => _flexiScenes.SingleOrDefault(x => x.Name == name);
    internal FlexiScene Next
    {
        get
        {
            if (Initial != null && Initial.NextFlexiScenes.Any())
            {
                var limitedScenes = new CircularReadOnlyList<FlexiScene> { Initial };
                foreach (var scene in Initial.NextFlexiScenes.Select(GetByName))
                {
                    if (scene != null)
                        limitedScenes.Add(scene);
                }
                var currentFlexiSceneIndex = limitedScenes.IndexOf(x => x.Name == CurrentFlexiScene);
                limitedScenes.CurrentIndex = currentFlexiSceneIndex;

                return limitedScenes.MoveNext;
            }
            else
            {
                var currentFlexiSceneIndex = All.IndexOf(x => x.Name == CurrentFlexiScene);
                _flexiScenes.CurrentIndex = currentFlexiSceneIndex;

                return _flexiScenes.MoveNext;
            }
        }
    }

    internal void Initialize(FlexiScene activeScene, FlexiScene? initialScene)
    {
        CurrentFlexiScene = activeScene.Name;

        if (initialScene != null)
            InitialFlexiScene = initialScene.Name;
    }


    internal FlexiScene SetCurrentScene(DateTimeOffset? now, FlexiScene scene, bool overwriteInitialScene = false)
    {
        if (now != null)
        {
            Changes.Add(new FlexiSceneChange { ChangedAt = now.Value, Scene = scene.Name });
            CleanUpOldChanges(now.Value);
        }

        CurrentFlexiScene = scene.Name;

        if (overwriteInitialScene)
            InitialFlexiScene = scene.Name;

        return Current;
    }

    internal void DeactivateScene(DateTimeOffset now)
    {
        if (CurrentFlexiScene != null)
        {
            Changes.Add(new FlexiSceneChange { ChangedAt = now, Scene = "Off" });
            CleanUpOldChanges(now);
        }

        CurrentFlexiScene = null;
        InitialFlexiScene = null;
    }

    internal FlexiScene? GetFlexiSceneToSimulate(DateTimeOffset? now)
    {
        var changeSince = now?.DayOfWeek switch
        {
            DayOfWeek.Tuesday => TimeSpan.FromDays(1),
            DayOfWeek.Thursday => TimeSpan.FromDays(1),
            _ => TimeSpan.FromDays(7),
        };

        var change = Changes.LastOrDefault(x => x.ChangedAt < now - changeSince);

        return change?.Scene is null or "Off" ? null : GetByName(change.Scene);
    }

    private void CleanUpOldChanges(DateTimeOffset now)
    {
        var changesToRemove = Changes.Where(x => x.ChangedAt < now - SaveChangesFor).ToList();
        var lastChange = changesToRemove.LastOrDefault();

        //keep last change before our interval as it is needed to know the state of x ago.
        foreach (var change in changesToRemove.Where(change => change != lastChange))
        {
            Changes.Remove(change);
        }
    }
}