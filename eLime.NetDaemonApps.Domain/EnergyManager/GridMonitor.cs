﻿using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
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


    public double CurrentLoad => CurrentPowerImport - CurrentPowerExport;

    public Double PeakLoad => (PeakImportSensor.State * 1000 ?? 0) > 2500
        ? PeakImportSensor.State * 1000 ?? 0
        : 2500;

    public Double CurrentAverageDemand => CurrentAverageDemandEntity.State * 1000 ?? 0;

    private readonly FixedSizeConcurrentQueue<(DateTimeOffset Moment, double Value)> _lastImportValues = new(200);
    private readonly FixedSizeConcurrentQueue<(DateTimeOffset Moment, double Value)> _lastExportValues = new(200);

    public GridMonitor(IScheduler scheduler, NumericEntity gridVoltageSensor, NumericSensor gridPowerImportSensor, NumericSensor gridPowerExportSensor, NumericEntity peakImportSensor, NumericEntity currentAverageDemandEntity)
    {
        _scheduler = scheduler;
        GridVoltageSensor = gridVoltageSensor;
        GridPowerImportSensor = gridPowerImportSensor;
        GridPowerImportSensor.Changed += GridPowerImportSensor_Changed;
        GridPowerExportSensor = gridPowerExportSensor;
        GridPowerExportSensor.Changed += GridPowerExportSensor_Changed;
        PeakImportSensor = peakImportSensor;
        CurrentAverageDemandEntity = currentAverageDemandEntity;
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

    public double AverageImportSince(DateTimeOffset now, TimeSpan timeSpan)
    {
        return Math.Round(_lastImportValues.Where(x => x.Moment.Add(timeSpan) > now).Select(x => x.Value).DefaultIfEmpty().Average());
    }

    public double AverageExportSince(DateTimeOffset now, TimeSpan timeSpan)
    {
        return Math.Round(_lastExportValues.Where(x => x.Moment.Add(timeSpan) > now).Select(x => x.Value).DefaultIfEmpty().Average());
    }

    public double AverageLoadSince(DateTimeOffset now, TimeSpan timeSpan)
    {
        return AverageImportSince(now, timeSpan) - AverageExportSince(now, timeSpan);
    }

    public void Dispose()
    {
        GridPowerImportSensor.Changed -= GridPowerImportSensor_Changed;
        GridPowerImportSensor.Dispose();

        GridPowerExportSensor.Changed -= GridPowerExportSensor_Changed;
        GridPowerExportSensor.Dispose();
    }
}