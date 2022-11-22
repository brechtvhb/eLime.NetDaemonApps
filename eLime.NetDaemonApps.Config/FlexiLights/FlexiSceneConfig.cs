namespace eLime.netDaemonApps.Config.FlexiLights;

public class FlexiSceneConfig
{
    public string Name { get; set; }
    public IList<ConditionConfig> Conditions { get; set; }
    public IList<ActionConfig> Actions { get; set; }
    public TimeSpan? TurnOffAfterIfTriggeredBySwitch { get; set; }
    public TimeSpan? TurnOffAfterIfTriggeredByMotionSensor { get; set; }

}