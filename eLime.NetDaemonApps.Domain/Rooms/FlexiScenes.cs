using eLime.NetDaemonApps.Domain.Helper;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.Rooms;

public class FlexiScenes
{
    private string? CurrentFlexiScene { get; set; }

    private readonly CircularReadOnlyList<FlexiScene> _flexiScenes = new();

    internal FlexiScenes(IEnumerable<FlexiScene> flexiScenes)
    {
        _flexiScenes.AddRange(flexiScenes);
    }

    public IReadOnlyList<FlexiScene> All => _flexiScenes.AsReadOnly();
    internal FlexiScene? GetSceneThatShouldActivate(IReadOnlyCollection<Entity> flexiSceneSensors) => All.FirstOrDefault(x => x.CanActivate(flexiSceneSensors));
    public FlexiScene? Current => All.SingleOrDefault(x => x.Name == CurrentFlexiScene);
    public FlexiScene? GetByName(String name) => _flexiScenes.SingleOrDefault(x => x.Name == name);

    internal FlexiScene Next
    {
        get
        {
            var currentFlexiSceneIndex = All.IndexOf(x => x.Name == CurrentFlexiScene);
            _flexiScenes.CurrentIndex = currentFlexiSceneIndex;

            return _flexiScenes.MoveNext;
        }
    }


    internal FlexiScene SetCurrentScene(FlexiScene scene)
    {
        CurrentFlexiScene = scene.Name;
        return Current;
    }

    internal void DeactivateScene()
    {
        CurrentFlexiScene = null;
    }
}