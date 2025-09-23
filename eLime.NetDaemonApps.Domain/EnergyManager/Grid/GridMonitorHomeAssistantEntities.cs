using eLime.NetDaemonApps.Domain.EnergyManager.BatteryManager;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;

namespace eLime.NetDaemonApps.Domain.EnergyManager.Grid;

public class GridMonitorHomeAssistantEntities(GridConfiguration config, BatteryManagerConfiguration batteryConfig) : IDisposable
{
    internal NumericSensor VoltageSensor = config.VoltageSensor;
    internal NumericSensor PowerImportSensor = config.ImportSensor;
    internal NumericSensor PowerExportSensor = config.ExportSensor;
    internal NumericSensor PeakImportSensor = config.PeakImportSensor;
    internal NumericSensor CurrentAverageDemandSensor = config.CurrentAverageDemandSensor;
    internal NumericSensor TotalBatteryChargePowerSensor = batteryConfig.TotalChargePowerSensor;
    internal NumericSensor TotalBatteryDischargePowerSensor = batteryConfig.TotalDischargePowerSensor;

    internal NumericSensor CurrentSolarPowerSensor = config.CurrentSolarPowerSensor;
    internal NumericSensor SolarForecastPowerNowSensor = config.SolarForecastPowerNowSensor;
    internal NumericSensor SolarForecastPower30MinutesSensor = config.SolarForecastPower30MinutesSensor;
    internal NumericSensor SolarForecastPower1HourSensor = config.SolarForecastPower1HourSensor;
    public void Dispose()
    {
        VoltageSensor.Dispose();
        PowerImportSensor.Dispose();
        PowerExportSensor.Dispose();
        PeakImportSensor.Dispose();
        CurrentAverageDemandSensor.Dispose();
        TotalBatteryChargePowerSensor.Dispose();
        TotalBatteryDischargePowerSensor.Dispose();
    }
}