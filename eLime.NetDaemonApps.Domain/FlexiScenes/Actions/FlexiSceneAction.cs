using eLime.NetDaemonApps.Domain.Entities.Select;

namespace eLime.NetDaemonApps.Domain.FlexiScenes.Actions;

public abstract class FlexiSceneAction(SelectEntity flexiScene) : Action
{
    public SelectEntity FlexiScene { get; init; } = flexiScene;
}

public class FlexiSceneTurnOnAction(SelectEntity flexiScene, string? flexiSceneToTrigger) : FlexiSceneAction(flexiScene)
{
    public string? FlexiSceneToTrigger { get; init; } = flexiSceneToTrigger;

    public override Task<bool?> Execute(bool detectStateChange = false)
    {
        if (FlexiScene.State == "Off" || FlexiScene.State == FlexiSceneToTrigger)
            FlexiScene.Change(FlexiSceneToTrigger ?? "Auto");

        bool? initialState = null;
        return Task.FromResult(initialState);
    }

    public override Action Reverse()
    {
        return null;
    }

}

public class FlexiSceneTurnOffAction(SelectEntity flexiScene, string? requiredFlexiScene) : FlexiSceneAction(flexiScene)
{
    public string? RequiredFlexiScene { get; init; } = requiredFlexiScene;

    public override Task<bool?> Execute(bool detectStateChange = false)
    {
        if (FlexiScene.State == "Off")
            return Task.FromResult<bool?>(null);

        if (!string.IsNullOrWhiteSpace(RequiredFlexiScene) && FlexiScene.State == RequiredFlexiScene)
            FlexiScene.Change("Off");
        else if (string.IsNullOrWhiteSpace(RequiredFlexiScene))
            FlexiScene.Change("Off");

        return Task.FromResult<bool?>(null);
    }
    public override Action Reverse()
    {
        return null;
    }
}