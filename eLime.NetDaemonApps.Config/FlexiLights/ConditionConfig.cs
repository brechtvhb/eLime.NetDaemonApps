namespace eLime.netDaemonApps.Config.FlexiLights;

public class ConditionConfig
{
    public string Binary { get; set; }
    public BinaryMethod BinaryMethod { get; set; }
    public List<ConditionConfig> Or { get; set; }
    public List<ConditionConfig> And { get; set; }

}

public enum BinaryMethod
{
    True,
    False
}