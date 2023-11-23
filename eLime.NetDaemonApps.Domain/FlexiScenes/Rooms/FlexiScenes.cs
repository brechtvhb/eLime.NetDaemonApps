using eLime.NetDaemonApps.Domain.Helper;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.FlexiScenes.Rooms;

public class FlexiScenes
{
    internal List<FlexiSceneChange> Changes { get; set; } = new();
    public TimeSpan SaveChangesFor = TimeSpan.FromDays(7);

    private string? CurrentFlexiScene { get; set; }

    //used when cycling through scenes
    private string? InitialFlexiScene { get; set; }

    private readonly CircularReadOnlyList<FlexiScene> _flexiScenes = new();

    internal FlexiScenes(IEnumerable<FlexiScene> flexiScenes)
    {
        _flexiScenes.AddRange(flexiScenes);
    }

    public IReadOnlyList<FlexiScene> All => _flexiScenes.AsReadOnly();
    internal FlexiScene? GetSceneThatShouldActivate(IReadOnlyCollection<Entity> flexiSceneSensors) => All.FirstOrDefault(x => x.CanActivate(flexiSceneSensors));
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

    private void CleanUpOldChanges(DateTimeOffset now)
    {
        var changesToRemove = Changes.Where(x => x.ChangedAt < now - SaveChangesFor);
        foreach (var change in changesToRemove)
            Changes.Remove(change);
    }
}