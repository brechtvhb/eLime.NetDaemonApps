using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using NetDaemon.HassModel;

namespace eLime.NetDaemonApps.Domain.EnergyManager.Grid;

public class GridConfiguration
{
    public GridConfiguration(IHaContext haContext, Config.EnergyManager.GridConfig config)
    {
        VoltageSensor = NumericSensor.Create(haContext, config.VoltageEntity);
        ImportSensor = NumericSensor.Create(haContext, config.ImportEntity);
        ExportSensor = NumericSensor.Create(haContext, config.ExportEntity);
        PeakImportSensor = NumericSensor.Create(haContext, config.PeakImportEntity);
        CurrentAverageDemandSensor = NumericSensor.Create(haContext, config.CurrentAverageDemandEntity);

        CurrentSolarPowerSensor = NumericSensor.Create(haContext, config.CurrentSolarPowerEntity);
        SolarForecastPowerNowSensor = NumericSensor.Create(haContext, config.SolarForecastPowerNowEntity);
        SolarForecastPower30MinutesSensor = NumericSensor.Create(haContext, config.SolarForecastPower30MinutesEntity);
        SolarForecastPower1HourSensor = NumericSensor.Create(haContext, config.SolarForecastPower1HourEntity);
    }
    public NumericSensor VoltageSensor { get; set; }
    public NumericSensor ImportSensor { get; set; }
    public NumericSensor ExportSensor { get; set; }
    public NumericSensor PeakImportSensor { get; set; }
    public NumericSensor CurrentAverageDemandSensor { get; set; }

    public NumericSensor CurrentSolarPowerSensor { get; set; }
    public NumericSensor SolarForecastPowerNowSensor { get; set; }
    public NumericSensor SolarForecastPower30MinutesSensor { get; set; }
    public NumericSensor SolarForecastPower1HourSensor { get; set; }
}