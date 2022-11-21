namespace FlexiLights.Config;

public class GatedActionConfig
{
    public String Name { get; set; }
    public IList<EvaluationConfig> Evaluations { get; set; }
    public IList<ActionConfig> Actions { get; set; }
    public TimeSpan? TurnOffAfterIfTriggeredBySwitch { get; set; }
    public TimeSpan? TurnOffAfterIfTriggeredByMotionSensor { get; set; }

}

public class EvaluationConfig
{
    public string Binary { get; set; }
    public BinaryMethod BinaryMethod { get; set; }
    public List<EvaluationConfig> Or { get; set; }
    public List<EvaluationConfig> And { get; set; }


}

public enum BinaryMethod
{
    True,
    False
}