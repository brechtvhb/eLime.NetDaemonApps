using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.Helper;
using NetDaemon.HassModel.Entities;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.EnergyManager;

public class GridMonitor : IDisposable, IGridMonitor
{
    private readonly IScheduler _scheduler;
    public NumericEntity GridVoltageSensor { get; }
    public NumericSensor GridPowerImportSensor { get; }
    public NumericSensor GridPowerExportSensor { get; }
    public NumericEntity PeakImportSensor { get; }
    public NumericEntity CurrentAverageDemandEntity { get; }

    public NumericSensor TotalBatteryChargePowerSensor { get; }
    public NumericSensor TotalBatteryDischargePowerSensor { get; }

    private double _lastKnownValidPowerImportValue;
    private double CurrentPowerImport
    {
        get
        {
            if (GridPowerImportSensor.State != null)
                _lastKnownValidPowerImportValue = GridPowerImportSensor.State.Value;

            return _lastKnownValidPowerImportValue;
        }
    }

    private double _lastKnownValidPowerExportValue;
    private double CurrentPowerExport
    {
        get
        {
            if (GridPowerExportSensor.State != null)
                _lastKnownValidPowerExportValue = GridPowerExportSensor.State.Value;

            return _lastKnownValidPowerExportValue;
        }
    }

    private double _lastKnownTotalBatteryChargePowerValue;
    private double CurrentBatteryChargePower
    {
        get
        {
            if (TotalBatteryChargePowerSensor.State != null)
                _lastKnownTotalBatteryChargePowerValue = TotalBatteryChargePowerSensor.State.Value;

            return _lastKnownTotalBatteryChargePowerValue;
        }
    }

    private double _lastKnownTotalBatteryDischargePower;
    private double CurrentBatteryDischargePower
    {
        get
        {
            if (TotalBatteryDischargePowerSensor.State != null)
                _lastKnownTotalBatteryDischargePower = TotalBatteryDischargePowerSensor.State.Value;

            return _lastKnownTotalBatteryDischargePower;
        }
    }


    public double CurrentLoad => CurrentPowerImport - CurrentPowerExport;
    public double CurrentLoadMinusBatteries => CurrentLoad - CurrentBatteryChargePower + CurrentBatteryDischargePower;

    public double PeakLoad => (PeakImportSensor.State * 1000 ?? 0) > 2500
        ? PeakImportSensor.State * 1000 ?? 0
        : 2500;

    public double CurrentAverageDemand => CurrentAverageDemandEntity.State * 1000 ?? 0;

    private readonly FixedSizeConcurrentQueue<(DateTimeOffset Moment, double Value)> _lastImportValues = new(200);
    private readonly FixedSizeConcurrentQueue<(DateTimeOffset Moment, double Value)> _lastExportValues = new(200);
    private readonly FixedSizeConcurrentQueue<(DateTimeOffset Moment, double Value)> _lastBatteryChargePowerValues = new(200);
    private readonly FixedSizeConcurrentQueue<(DateTimeOffset Moment, double Value)> _lastBatteryDischargePowerValues = new(200);

    public GridMonitor(IScheduler scheduler, NumericEntity gridVoltageSensor, NumericSensor gridPowerImportSensor, NumericSensor gridPowerExportSensor, NumericEntity peakImportSensor, NumericEntity currentAverageDemandEntity, NumericSensor totalBatteryChargePowerSensor, NumericSensor totalBatteryDischargePowerSensor)
    {
        _scheduler = scheduler;
        GridVoltageSensor = gridVoltageSensor;

        GridPowerImportSensor = gridPowerImportSensor;
        GridPowerImportSensor.Changed += GridPowerImportSensor_Changed;

        GridPowerExportSensor = gridPowerExportSensor;
        GridPowerExportSensor.Changed += GridPowerExportSensor_Changed;

        PeakImportSensor = peakImportSensor;
        CurrentAverageDemandEntity = currentAverageDemandEntity;

        TotalBatteryChargePowerSensor = totalBatteryChargePowerSensor;
        TotalBatteryChargePowerSensor.Changed += TotalBatteryChargePowerSensor_Changed;

        TotalBatteryDischargePowerSensor = totalBatteryDischargePowerSensor;
        TotalBatteryDischargePowerSensor.Changed += TotalBatteryDischargePowerSensor_Changed;
    }

    private void GridPowerImportSensor_Changed(object? sender, NumericSensorEventArgs e)
    {
        if (e.Sensor.State == null)
            return;

        _lastImportValues.Enqueue((_scheduler.Now, e.Sensor.State.Value));
    }
    private void GridPowerExportSensor_Changed(object? sender, NumericSensorEventArgs e)
    {
        if (e.Sensor.State == null)
            return;

        _lastExportValues.Enqueue((_scheduler.Now, e.Sensor.State.Value));
    }

    private void TotalBatteryChargePowerSensor_Changed(object? sender, NumericSensorEventArgs e)
    {
        if (e.Sensor.State == null)
            return;

        _lastBatteryChargePowerValues.Enqueue((_scheduler.Now, e.Sensor.State.Value));
    }
    private void TotalBatteryDischargePowerSensor_Changed(object? sender, NumericSensorEventArgs e)
    {
        if (e.Sensor.State == null)
            return;

        _lastBatteryDischargePowerValues.Enqueue((_scheduler.Now, e.Sensor.State.Value));
    }
    public double AverageImportSince(DateTimeOffset now, TimeSpan timeSpan)
    {
        var values = _lastImportValues.Where(x => x.Moment.Add(timeSpan) > now).Select(x => x.Value).DefaultIfEmpty().ToList();
        return values.Count == 0 ? CurrentBatteryChargePower : Math.Round(values.Average());
    }

    public double AverageExportSince(DateTimeOffset now, TimeSpan timeSpan)
    {
        var values = _lastExportValues.Where(x => x.Moment.Add(timeSpan) > now).Select(x => x.Value).DefaultIfEmpty().ToList();
        return values.Count == 0 ? CurrentBatteryChargePower : Math.Round(values.Average());
    }

    public double AverageBatteryChargePowerSince(DateTimeOffset now, TimeSpan timeSpan)
    {
        var values = _lastBatteryChargePowerValues.Where(x => x.Moment.Add(timeSpan) > now).Select(x => x.Value).DefaultIfEmpty().ToList();
        return values.Count == 0 ? CurrentBatteryChargePower : Math.Round(values.Average());
    }

    public double AverageBatteryDischargePowerSince(DateTimeOffset now, TimeSpan timeSpan)
    {
        var values = _lastBatteryDischargePowerValues.Where(x => x.Moment.Add(timeSpan) > now).Select(x => x.Value).DefaultIfEmpty().ToList();
        return values.Count == 0 ? CurrentBatteryChargePower : Math.Round(values.Average());
    }

    public double AverageLoadSince(DateTimeOffset now, TimeSpan timeSpan) => AverageImportSince(now, timeSpan) - AverageExportSince(now, timeSpan);
    public double AverageLoadMinusBatteriesSince(DateTimeOffset now, TimeSpan timeSpan) => AverageLoadSince(now, timeSpan) - AverageBatteryChargePowerSince(now, timeSpan) + AverageBatteryDischargePowerSince(now, timeSpan);

    public void Dispose()
    {
        GridPowerImportSensor.Changed -= GridPowerImportSensor_Changed;
        GridPowerImportSensor.Dispose();

        GridPowerExportSensor.Changed -= GridPowerExportSensor_Changed;
        GridPowerExportSensor.Dispose();

        TotalBatteryChargePowerSensor.Changed -= TotalBatteryChargePowerSensor_Changed;
        TotalBatteryChargePowerSensor.Dispose();

        TotalBatteryDischargePowerSensor.Changed -= TotalBatteryDischargePowerSensor_Changed;
        TotalBatteryDischargePowerSensor.Dispose();
    }
}