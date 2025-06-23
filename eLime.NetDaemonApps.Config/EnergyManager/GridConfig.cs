namespace eLime.NetDaemonApps.Config.EnergyManager;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

public class GridConfig
{
    public string VoltageEntity { get; set; }
    public string ImportEntity { get; set; }
    public string ExportEntity { get; set; }
    public string PeakImportEntity { get; set; }
    public string CurrentAverageDemandEntity { get; set; }
}