namespace FlexiLights.Config;

public class RoomConfig
{
    public String Name { get; set; } //OK
    public IList<String> Lights { get; set; }
    public Boolean AutoTransition { get; set; }
    public IList<String>? IlluminanceSensors { get; set; }
    public Int32? IlluminanceThreshold { get; set; }
    public Boolean AutoSwitchOffAboveIlluminance { get; set; }
    public IList<String>? MotionSensors { get; set; }
    public TimeSpan? IgnorePresenceAfterOffDuration { get; set; }
    public IList<String>? Switches { get; set; }
    public TimeSpan? ClickInterval { get; set; }
    public ClickBehaviour ClickBehaviour { get; set; }
    public TimeSpan? LongClickDuration { get; set; }
    public TimeSpan? UberLongClickDuration { get; set; }
    public IList<String>? OffSensors { get; set; }
    public IList<GatedActionConfig> GatedActions { get; set; }
    public IList<ActionConfig>? DoubleClickActions { get; set; }
    public IList<ActionConfig>? TripleClickActions { get; set; }
    public IList<ActionConfig>? LongClickActions { get; set; }
    public IList<ActionConfig>? UberLongClickActions { get; set; }
    public IList<ActionConfig> OffActions { get; set; }
}

public enum ClickBehaviour
{
    ChangeOffDurationOnly,
    ChangeOFfDurationAndGoToNextGatedActions,
}