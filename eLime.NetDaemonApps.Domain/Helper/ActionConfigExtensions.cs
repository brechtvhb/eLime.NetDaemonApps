using eLime.NetDaemonApps.Config.FlexiLights;
using eLime.NetDaemonApps.Domain.Actions;
using eLime.NetDaemonApps.Domain.BinarySensors;
using eLime.NetDaemonApps.Domain.Lights;
using eLime.NetDaemonApps.Domain.Rooms.Actions;
using eLime.NetDaemonApps.Domain.Scenes;
using NetDaemon.HassModel;
using Action = eLime.NetDaemonApps.Domain.Rooms.Actions.Action;

namespace eLime.NetDaemonApps.Domain.Helper;

internal static class ActionConfigExtensions
{
    internal static List<Action> ConvertToDomainModel(this IList<ActionConfig>? actions, IHaContext context)
    {
        var actionList = new List<Action>();

        if (actions == null || !actions.Any())
            return actionList;

        foreach (var actionConfig in actions)
        {
            var action = actionConfig.ConvertToDomainModel(context);
            actionList.Add(action);
        }

        return actionList;
    }

    public static Action ConvertToDomainModel(this ActionConfig config, IHaContext haContext)
    {
        return config switch
        {
            { ExecuteOffActions: true } => config.ConvertToExecuteOffActionsActionDomainModel(haContext),
            { Light: not null, LightAction: not Config.FlexiLights.LightAction.Unknown } => config.ConvertToLightActionDomainModel(haContext),
            { Lights: not null, LightAction: not Config.FlexiLights.LightAction.Unknown } => config.ConvertToLightActionDomainModel(haContext),
            { Scene: not null } => config.ConvertToSceneActionDomainModel(haContext),
            { Switch: not null, SwitchAction: not SwitchAction.Unknown } => config.ConvertToSwitchActionDomainModel(haContext),
            _ => throw new ArgumentException("invalid action configuration")
        };
    }


    internal static Action ConvertToLightActionDomainModel(this ActionConfig config, IHaContext haContext)
    {
        if ((config.Light == null && config.Lights == null) || config.LightAction == Config.FlexiLights.LightAction.Unknown)
            throw new ArgumentException("Light or light action not set");

        var lights = new List<Light>();

        if (config.Light != null)
        {
            lights.Add(new Light(haContext, config.Light));
        }
        else
        {
            lights.AddRange(config.Lights.Select(x => new Light(haContext, x)));
        }

        return config.LightAction switch
        {
            Config.FlexiLights.LightAction.TurnOn => new LightTurnOnAction(lights, config.TransitionDuration ?? TimeSpan.FromSeconds(2), config.AutoTransitionDuration, config.Profile, config.Color, config.Brightness, config.Flash, config.Effect),
            Config.FlexiLights.LightAction.TurnOff => new LightTurnOffAction(lights, config.TransitionDuration ?? TimeSpan.FromSeconds(2), config.AutoTransitionDuration),
        };
    }

    internal static Action ConvertToSwitchActionDomainModel(this ActionConfig config, IHaContext haContext)
    {
        if (config.Switch == null || config.SwitchAction == SwitchAction.Unknown)
            throw new ArgumentException("Switch or switch action not set");

        var @switch = new Switch(haContext, config.Switch);

        return config.SwitchAction switch
        {
            SwitchAction.TurnOn => new SwitchTurnOnAction(@switch),
            SwitchAction.TurnOff => new SwitchTurnOffAction(@switch),
            SwitchAction.Pulse => new SwitchPulseAction(@switch, config.PulseDuration),
        };
    }

    internal static Action ConvertToSceneActionDomainModel(this ActionConfig config, IHaContext haContext)
    {
        if (config.Scene == null)
            throw new ArgumentNullException(nameof(config.Scene), "Scene not set");

        var scene = new Scene(haContext, config.Scene);

        return new SceneTurnOnAction(scene, config.TransitionDuration ?? TimeSpan.FromSeconds(2), config.AutoTransitionDuration);
    }

    internal static Action ConvertToExecuteOffActionsActionDomainModel(this ActionConfig config, IHaContext haContext)
    {
        if (!config.ExecuteOffActions)
            throw new ArgumentException("ExecuteOffActions not set");

        return new ExecuteOffActionsAction();
    }

}