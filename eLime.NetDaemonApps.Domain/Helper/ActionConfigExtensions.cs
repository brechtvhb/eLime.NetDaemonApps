using eLime.NetDaemonApps.Config.FlexiLights;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.Lights;
using eLime.NetDaemonApps.Domain.Entities.Scripts;
using eLime.NetDaemonApps.Domain.Entities.Select;
using eLime.NetDaemonApps.Domain.Entities.TextSensors;
using eLime.NetDaemonApps.Domain.FlexiScenes.Actions;
using eLime.NetDaemonApps.Domain.Scenes;
using NetDaemon.HassModel;
using Action = eLime.NetDaemonApps.Domain.FlexiScenes.Actions.Action;
using FlexiSceneAction = eLime.NetDaemonApps.Config.FlexiLights.FlexiSceneAction;

namespace eLime.NetDaemonApps.Domain.Helper;

internal static class SwitchConfigExtensions
{
    internal static List<ISwitch> ConvertToDomainModel(this IList<SwitchConfig>? switches, TimeSpan? clickInterval, TimeSpan? longClickDuration, TimeSpan? uberLongClickDuration,
        string? singlePressState, string? doublePressState, string? triplePressState, string? longPressState, string? uberLongPressState, IHaContext context)
    {
        var switchList = new List<ISwitch>();

        if (switches == null || !switches.Any())
            return switchList;

        foreach (var switchConfig in switches)
        {
            ISwitch sensor = switchConfig switch
            {
                { Binary: not null } => BinarySwitch.Create(context, switchConfig.Binary, clickInterval, longClickDuration, uberLongClickDuration),
                { State: not null } => StateSwitch.Create(context, switchConfig.State, singlePressState, doublePressState, triplePressState, longPressState, uberLongPressState),
                _ => throw new ArgumentException("invalid switch configuration")
            };

            if (switchList.Any(x => x.EntityId == sensor.EntityId))
                continue;

            switchList.Add(sensor);
        }

        return switchList;
    }
}

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
            { Scene: not null } => config.ConvertToSceneActionDomainModel(haContext),
            { Script: not null } => config.ConvertToScriptActionDomainModel(haContext),
            { Switch: not null, SwitchAction: not SwitchAction.Unknown } => config.ConvertToSwitchActionDomainModel(haContext),
            { FlexiScene: not null, FlexiSceneAction: not Config.FlexiLights.FlexiSceneAction.Unknown } => config.ConvertFlexiSceneActionToDomainModel(haContext),
            _ => throw new ArgumentException("invalid action configuration")
        };
    }

    internal static Action ConvertToLightActionDomainModel(this ActionConfig config, IHaContext haContext)
    {
        if (config.Light == null || config.LightAction == Config.FlexiLights.LightAction.Unknown)
            throw new ArgumentException("Light or light action not set");

        var light = new Light(haContext, config.Light);

        return config.LightAction switch
        {
            Config.FlexiLights.LightAction.TurnOn => new LightTurnOnAction(light, config.Profile, config.Color, config.Brightness, config.Flash, config.Effect),
            Config.FlexiLights.LightAction.TurnOff => new LightTurnOffAction(light)
        };
    }

    internal static Action ConvertToSwitchActionDomainModel(this ActionConfig config, IHaContext haContext)
    {
        if (config.Switch == null || config.SwitchAction == SwitchAction.Unknown)
            throw new ArgumentException("Switch or switch action not set");

        var @switch = new BinarySwitch(haContext, config.Switch);

        return config.SwitchAction switch
        {
            SwitchAction.TurnOn => new SwitchTurnOnAction(@switch),
            SwitchAction.TurnOff => new SwitchTurnOffAction(@switch),
            SwitchAction.Toggle => new SwitchToggleAction(@switch),
            SwitchAction.Pulse => new SwitchPulseAction(@switch, config.PulseDuration),
        };
    }

    internal static Action ConvertToScriptActionDomainModel(this ActionConfig config, IHaContext haContext)
    {
        if (config.Script == null)
            throw new ArgumentException("Service not set");

        var script = new Script(haContext);

        return config.Script switch
        {
            "script.momentary_switch" => new MomentarySwitchAction(script, config.ScriptData?["entityId"]),
            "script.wake_up_pc" => new WakeUpPcAction(script, config.ScriptData?["macAddress"], config.ScriptData?.SingleOrDefault(x => x.Key == "broadcastAddress").Value),
            _ => throw new ArgumentOutOfRangeException()
        };
    }
    internal static Action ConvertToSceneActionDomainModel(this ActionConfig config, IHaContext haContext)
    {
        if (config.Scene == null)
            throw new ArgumentNullException(nameof(config.Scene), "Scene not set");

        var scene = new Scene(haContext, config.Scene);

        return new SceneTurnOnAction(scene);
    }

    internal static Action ConvertFlexiSceneActionToDomainModel(this ActionConfig config, IHaContext haContext)
    {
        if (config.FlexiScene == null || config.FlexiSceneAction == FlexiSceneAction.Unknown)
            throw new ArgumentException("FlexiScene or FlexiSceneToTrigger not set");

        var flexiScene = new SelectEntity(haContext, config.FlexiScene);

        return config.FlexiSceneAction switch
        {
            Config.FlexiLights.FlexiSceneAction.TurnOn => new FlexiSceneTurnOnAction(flexiScene, config.FlexiSceneToTrigger),
            Config.FlexiLights.FlexiSceneAction.TurnOff => new FlexiSceneTurnOffAction(flexiScene, config.RequiredFlexiScene)
        };
    }

    internal static Action ConvertToExecuteOffActionsActionDomainModel(this ActionConfig config, IHaContext haContext)
    {
        if (!config.ExecuteOffActions)
            throw new ArgumentException("ExecuteOffActions not set");

        return new ExecuteOffActionsAction();
    }

}