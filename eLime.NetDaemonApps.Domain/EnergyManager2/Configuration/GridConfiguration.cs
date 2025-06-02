using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using NetDaemon.HassModel;

namespace eLime.NetDaemonApps.Domain.EnergyManager2.Configuration;

public class GridConfiguration
{
    public GridConfiguration(IHaContext haContext, Config.EnergyManager.GridConfig config)
    {
        VoltageSensor = NumericSensor.Create(haContext, config.VoltageEntity);
        ImportSensor = NumericSensor.Create(haContext, config.ImportEntity);
        ExportSensor = NumericSensor.Create(haContext, config.ExportEntity);
        PeakImportSensor = NumericSensor.Create(haContext, config.PeakImportEntity);
        CurrentAverageDemandSensor = NumericSensor.Create(haContext, config.CurrentAverageDemandEntity);
    }
    public NumericSensor VoltageSensor { get; set; }
    public NumericSensor ImportSensor { get; set; }
    public NumericSensor ExportSensor { get; set; }
    public NumericSensor PeakImportSensor { get; set; }
    public NumericSensor CurrentAverageDemandSensor { get; set; }
}