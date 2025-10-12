using eLime.NetDaemonApps.Domain.Entities.Select;

namespace eLime.NetDaemonApps.Domain.FlexiScenes.Actions;

public class FlexiSceneAction : Action
{
    public SelectEntity FlexiScene { get; init; }
    public string FlexiSceneToTrigger { get; init; }

    public FlexiSceneAction(SelectEntity flexiScene, string flexiSceneToTrigger)
    {
        FlexiScene = flexiScene;
        FlexiSceneToTrigger = flexiSceneToTrigger;
    }

    public override Task<bool?> Execute(bool detectStateChange = false)
    {
        if (FlexiScene.State == "Off")
            FlexiScene.Change(FlexiSceneToTrigger);

        bool? initialState = null;
        return Task.FromResult(initialState);
    }

    public override Action Reverse()
    {
        return null;
    }
}