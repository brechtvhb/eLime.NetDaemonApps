using eLime.NetDaemonApps.Domain.Scenes;
using Action = eLime.NetDaemonApps.Domain.FlexiScenes.Actions.Action;

namespace eLime.NetDaemonApps.Domain.FlexiScenes.Actions;

public class SceneTurnOnAction : Action
{
    public Scene Scene { get; init; }
    public TimeSpan? TransitionDuration { get; init; }
    public TimeSpan? AutoTransitionDuration { get; init; }

    public SceneTurnOnAction(Scene scene, TimeSpan? transitionDuration, TimeSpan? autoTransitionDuration)
    {
        Scene = scene;
        TransitionDuration = transitionDuration;
        AutoTransitionDuration = autoTransitionDuration;
    }

    public override Task Execute(bool isAutoTransition = false)
    {
        var transitionDuration = (long?)(isAutoTransition ? AutoTransitionDuration ?? TransitionDuration : TransitionDuration)?.TotalSeconds;

        Scene.TurnOn(new SceneTurnOnParameters
        {
            Transition = transitionDuration
        });

        return Task.CompletedTask;
    }
}