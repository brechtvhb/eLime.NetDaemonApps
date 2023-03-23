namespace eLime.NetDaemonApps.Config.SmartIrrigation;

public class ClassicIrrigationConfig
{
    public string SoilMoistureEntity { get; set; }
    public int CriticalSoilMoisture { get; set; }
    public int LowSoilMoisture { get; set; }
    public int TargetSoilMoisture { get; set; }
    public TimeSpan? MaxDuration { get; set; }
    public TimeOnly? IrrigationStartWindow { get; set; }
    public TimeOnly? IrrigationEndWindow { get; set; }
}