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

    internal IReadOnlyList<FlexiScene> All => _flexiScenes.AsReadOnly();
    internal FlexiScene? GetSceneThatShouldActivate(IReadOnlyCollection<Entity> flexiSceneSensors) => All.FirstOrDefault(x => x.CanActivate(flexiSceneSensors));
    internal FlexiScene? Current => All.SingleOrDefault(x => x.Name == CurrentFlexiScene);

    internal FlexiScene Next
    {
        get
        {
            var currentFlexiSceneIndex = All.IndexOf(x => x.Name == CurrentFlexiScene);
            _flexiScenes.CurrentIndex = currentFlexiSceneIndex;

            return _flexiScenes.MoveNext;
        }
    }


    public FlexiScene SetCurrentScene(FlexiScene scene)
    {
        CurrentFlexiScene = scene.Name;
        return Current;
    }

    public void DeactivateScene()
    {
        CurrentFlexiScene = null;
    }
}