using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.Helper;

namespace eLime.NetDaemonApps.Domain.EnergyManager.Grid;
#pragma warning disable CS8618, CS9264

public class GridMonitor : IDisposable, IGridMonitor
{
    internal EnergyManagerContext Context { get; private set; }
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

    public double PeakLoad => (HomeAssistant.PeakImportSensor.State * 1000 ?? 0) > 2500
        ? HomeAssistant.PeakImportSensor.State * 1000 ?? 0
        : 2500;

    public double CurrentAverageDemand => HomeAssistant.CurrentAverageDemandSensor.State * 1000 ?? 0;

    //600 = at least 10 minutes of data (as we receive max one update per second).
    private readonly FixedSizeConcurrentQueue<(DateTimeOffset Moment, double Value)> _lastImportValues = new(600);
    private readonly FixedSizeConcurrentQueue<(DateTimeOffset Moment, double Value)> _lastExportValues = new(600);
    private readonly FixedSizeConcurrentQueue<(DateTimeOffset Moment, double Value)> _lastBatteryChargePowerValues = new(600);
    private readonly FixedSizeConcurrentQueue<(DateTimeOffset Moment, double Value)> _lastBatteryDischargePowerValues = new(600);

    public static GridMonitor Create(EnergyManagerConfiguration configuration)
    {
        var gridMonitor2 = new GridMonitor();
        gridMonitor2.Initialize(configuration);
        return gridMonitor2;
    }


    private void Initialize(EnergyManagerConfiguration configuration)
    {
        Context = configuration.Context;
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

        _lastImportValues.Enqueue((Context.Scheduler.Now, e.Sensor.State.Value));
    }
    private void GridPowerExportSensor_Changed(object? sender, NumericSensorEventArgs e)
    {
        if (e.Sensor.State == null)
            return;

        _lastExportValues.Enqueue((Context.Scheduler.Now, e.Sensor.State.Value));
    }

    private void TotalBatteryChargePowerSensor_Changed(object? sender, NumericSensorEventArgs e)
    {
        if (e.Sensor.State == null)
            return;

        _lastBatteryChargePowerValues.Enqueue((Context.Scheduler.Now, e.Sensor.State.Value));
    }
    private void TotalBatteryDischargePowerSensor_Changed(object? sender, NumericSensorEventArgs e)
    {
        if (e.Sensor.State == null)
            return;

        _lastBatteryDischargePowerValues.Enqueue((Context.Scheduler.Now, e.Sensor.State.Value));
    }

    public double AverageImport(TimeSpan timeSpan)
    {
        var values = _lastImportValues.Where(x => x.Moment.Add(timeSpan) > Context.Scheduler.Now).Select(x => x.Value).ToList();
        return values.Count == 0 ? CurrentPowerImport : Math.Round(values.Average());
    }

    public double AverageExport(TimeSpan timeSpan)
    {
        var values = _lastExportValues.Where(x => x.Moment.Add(timeSpan) > Context.Scheduler.Now).Select(x => x.Value).ToList();
        return values.Count == 0 ? CurrentPowerExport : Math.Round(values.Average());
    }

    public double AverageBatteriesChargingPower(TimeSpan timeSpan)
    {
        var values = _lastBatteryChargePowerValues.Where(x => x.Moment.Add(timeSpan) > Context.Scheduler.Now).Select(x => x.Value).ToList();
        return values.Count == 0 ? CurrentBatteryChargePower : Math.Round(values.Average());
    }

    public double AverageBatteriesDischargingPower(TimeSpan timeSpan)
    {
        var values = _lastBatteryDischargePowerValues.Where(x => x.Moment.Add(timeSpan) > Context.Scheduler.Now).Select(x => x.Value).ToList();
        return values.Count == 0 ? CurrentBatteryDischargePower : Math.Round(values.Average());
    }


    public double AverageLoad(TimeSpan timeSpan) => AverageImport(timeSpan) - AverageExport(timeSpan);
    public double AverageBatteriesLoad(TimeSpan timeSpan) => AverageBatteriesChargingPower(timeSpan) - AverageBatteriesDischargingPower(timeSpan);
    public double AverageLoadMinusBatteries(TimeSpan timeSpan) => AverageLoad(timeSpan) - AverageBatteriesLoad(timeSpan);

    public void Dispose()
    {
        HomeAssistant.PowerImportSensor.Changed -= GridPowerImportSensor_Changed;
        HomeAssistant.PowerExportSensor.Changed -= GridPowerExportSensor_Changed;
        HomeAssistant.TotalBatteryChargePowerSensor.Changed -= TotalBatteryChargePowerSensor_Changed;
        HomeAssistant.TotalBatteryDischargePowerSensor.Changed -= TotalBatteryDischargePowerSensor_Changed;

        HomeAssistant.Dispose();
    }
}