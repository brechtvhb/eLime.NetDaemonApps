using eLime.NetDaemonApps.Domain.EnergyManager2.Configuration;
using eLime.NetDaemonApps.Domain.EnergyManager2.HomeAssistant;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.Helper;
using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.EnergyManager2;
#pragma warning disable CS8618, CS9264

public class GridMonitor2 : IDisposable, IGridMonitor2
{
    internal ILogger Logger { get; private set; }
    internal IScheduler Scheduler { get; private set; }
    internal GridMonitorHomeAssistantEntities HomeAssistant { get; private set; }

    private double _lastKnownValidPowerImportValue;
    private double CurrentPowerImport
    {
        get
        {
            if (HomeAssistant.PowerImportSensor.State != null)
                _lastKnownValidPowerImportValue = HomeAssistant.PowerImportSensor.State.Value;

            return _lastKnownValidPowerImportValue;
        }
    }

    private double _lastKnownValidPowerExportValue;
    private double CurrentPowerExport
    {
        get
        {
            if (HomeAssistant.PowerExportSensor.State != null)
                _lastKnownValidPowerExportValue = HomeAssistant.PowerExportSensor.State.Value;

            return _lastKnownValidPowerExportValue;
        }
    }

    private double _lastKnownTotalBatteryChargePowerValue;
    private double CurrentBatteryChargePower
    {
        get
        {
            if (HomeAssistant.TotalBatteryChargePowerSensor.State != null)
                _lastKnownTotalBatteryChargePowerValue = HomeAssistant.TotalBatteryChargePowerSensor.State.Value;

            return _lastKnownTotalBatteryChargePowerValue;
        }
    }

    private double _lastKnownTotalBatteryDischargePower;
    private double CurrentBatteryDischargePower
    {
        get
        {
            if (HomeAssistant.TotalBatteryDischargePowerSensor.State != null)
                _lastKnownTotalBatteryDischargePower = HomeAssistant.TotalBatteryDischargePowerSensor.State.Value;

            return _lastKnownTotalBatteryDischargePower;
        }
    }


    public double CurrentLoad => CurrentPowerImport - CurrentPowerExport;
    public double CurrentLoadMinusBatteries => CurrentLoad - CurrentBatteryChargePower + CurrentBatteryDischargePower;

    public Double PeakLoad => (HomeAssistant.PeakImportSensor.State * 1000 ?? 0) > 2500
        ? HomeAssistant.PeakImportSensor.State * 1000 ?? 0
        : 2500;

    public Double CurrentAverageDemand => HomeAssistant.CurrentAverageDemandSensor.State * 1000 ?? 0;

    private readonly FixedSizeConcurrentQueue<(DateTimeOffset Moment, double Value)> _lastImportValues = new(200);
    private readonly FixedSizeConcurrentQueue<(DateTimeOffset Moment, double Value)> _lastExportValues = new(200);
    private readonly FixedSizeConcurrentQueue<(DateTimeOffset Moment, double Value)> _lastBatteryChargePowerValues = new(200);
    private readonly FixedSizeConcurrentQueue<(DateTimeOffset Moment, double Value)> _lastBatteryDischargePowerValues = new(200);

    public static GridMonitor2 Create(EnergyManagerConfiguration configuration)
    {
        var gridMonitor2 = new GridMonitor2();
        gridMonitor2.Initialize(configuration);
        return gridMonitor2;
    }


    private void Initialize(EnergyManagerConfiguration configuration)
    {
        Logger = configuration.Logger;
        Scheduler = configuration.Scheduler;
        HomeAssistant = new GridMonitorHomeAssistantEntities(configuration.Grid, configuration.BatteryManager);
        HomeAssistant.PowerImportSensor.Changed += GridPowerImportSensor_Changed;
        HomeAssistant.PowerExportSensor.Changed += GridPowerExportSensor_Changed;
        HomeAssistant.TotalBatteryChargePowerSensor.Changed += TotalBatteryChargePowerSensor_Changed;
        HomeAssistant.TotalBatteryDischargePowerSensor.Changed += TotalBatteryDischargePowerSensor_Changed;
    }


    private void GridPowerImportSensor_Changed(object? sender, NumericSensorEventArgs e)
    {
        if (e.Sensor.State == null)
            return;

        _lastImportValues.Enqueue((Scheduler.Now, e.Sensor.State.Value));
    }
    private void GridPowerExportSensor_Changed(object? sender, NumericSensorEventArgs e)
    {
        if (e.Sensor.State == null)
            return;

        _lastExportValues.Enqueue((Scheduler.Now, e.Sensor.State.Value));
    }

    private void TotalBatteryChargePowerSensor_Changed(object? sender, NumericSensorEventArgs e)
    {
        if (e.Sensor.State == null)
            return;

        _lastBatteryChargePowerValues.Enqueue((Scheduler.Now, e.Sensor.State.Value));
    }
    private void TotalBatteryDischargePowerSensor_Changed(object? sender, NumericSensorEventArgs e)
    {
        if (e.Sensor.State == null)
            return;

        _lastBatteryDischargePowerValues.Enqueue((Scheduler.Now, e.Sensor.State.Value));
    }

    public double AverageImportSince(DateTimeOffset now, TimeSpan timeSpan) => Math.Round(_lastImportValues.Where(x => x.Moment.Add(timeSpan) > now).Select(x => x.Value).DefaultIfEmpty().Average());

    public double AverageExportSince(DateTimeOffset now, TimeSpan timeSpan) => Math.Round(_lastExportValues.Where(x => x.Moment.Add(timeSpan) > now).Select(x => x.Value).DefaultIfEmpty().Average());
    public double AverageBatteryChargePowerSince(DateTimeOffset now, TimeSpan timeSpan) => Math.Round(_lastBatteryChargePowerValues.Where(x => x.Moment.Add(timeSpan) > now).Select(x => x.Value).DefaultIfEmpty().Average());
    public double AverageBatteryDischargePowerSince(DateTimeOffset now, TimeSpan timeSpan) => Math.Round(_lastBatteryDischargePowerValues.Where(x => x.Moment.Add(timeSpan) > now).Select(x => x.Value).DefaultIfEmpty().Average());


    public double AverageLoadSince(DateTimeOffset now, TimeSpan timeSpan) => AverageImportSince(now, timeSpan) - AverageExportSince(now, timeSpan);
    public double AverageLoadMinusBatteriesSince(DateTimeOffset now, TimeSpan timeSpan) => AverageLoadSince(now, timeSpan) - AverageBatteryChargePowerSince(now, timeSpan) + AverageBatteryDischargePowerSince(now, timeSpan);

    public void Dispose()
    {
        HomeAssistant.PowerImportSensor.Changed -= GridPowerImportSensor_Changed;
        HomeAssistant.PowerExportSensor.Changed -= GridPowerExportSensor_Changed;
        HomeAssistant.TotalBatteryChargePowerSensor.Changed -= TotalBatteryChargePowerSensor_Changed;
        HomeAssistant.TotalBatteryDischargePowerSensor.Changed -= TotalBatteryDischargePowerSensor_Changed;

        HomeAssistant.Dispose();
    }
}