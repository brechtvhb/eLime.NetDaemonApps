namespace eLime.NetDaemonApps.Config.FlexiLights;

public class RoomConfig
{
    public string Name { get; set; } //OK
    public bool? Enabled { get; set; }
    public bool AutoTransition { get; set; }
    public bool AutoTransitionTurnOffIfNoValidSceneFound { get; set; }
    public IList<string>? IlluminanceSensors { get; set; }
    public int? IlluminanceThreshold { get; set; }
    public bool AutoSwitchOffAboveIlluminance { get; set; }
    public IList<string>? MotionSensors { get; set; }
    public TimeSpan? IgnorePresenceAfterOffDuration { get; set; }
    public IList<SwitchConfig>? Switches { get; set; }
    public TimeSpan? ClickInterval { get; set; }
    public TimeSpan? LongClickDuration { get; set; }
    public TimeSpan? UberLongClickDuration { get; set; }
    public String? SinglePressState { get; set; }
    public String? DoublePressState { get; set; }
    public String? TriplePressState { get; set; }
    public String? LongPressState { get; set; }
    public String? UberLongPressState { get; set; }

    public InitialClickAfterMotionBehaviour InitialClickAfterMotionBehaviour { get; set; }
    public IList<string>? OffSensors { get; set; }
    public IList<FlexiSceneConfig> FlexiScenes { get; set; }
    public IList<ActionConfig>? DoubleClickActions { get; set; }
    public IList<ActionConfig>? TripleClickActions { get; set; }
    public IList<ActionConfig>? LongClickActions { get; set; }
    public IList<ActionConfig>? UberLongClickActions { get; set; }
    public IList<ActionConfig> OffActions { get; set; }
}

public enum InitialClickAfterMotionBehaviour
{
    ChangeOFfDurationAndGoToNextAutomation,
    ChangeOffDurationOnly,
}