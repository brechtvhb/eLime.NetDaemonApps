using eLime.NetDaemonApps.Domain.Scenes;

namespace eLime.NetDaemonApps.Domain.FlexiScenes.Actions;

public class SceneTurnOnAction : Action
{
    public Scene Scene { get; init; }

    public SceneTurnOnAction(Scene scene)
    {
        Scene = scene;
    }

    public override Task<bool?> Execute(bool detectStateChange = false)
    {
        Scene.TurnOn(new SceneTurnOnParameters
        {
        });

        bool? initialState = null;
        return Task.FromResult(initialState);
    }

    public override Action Reverse()
    {
        return null;
    }
}