namespace eLime.NetDaemonApps.Config.FlexiLights;

public class ConditionConfig
{
    public string Binary { get; set; }
    public string Numeric { get; set; }
    public List<ConditionConfig> Or { get; set; }
    public List<ConditionConfig> And { get; set; }

}