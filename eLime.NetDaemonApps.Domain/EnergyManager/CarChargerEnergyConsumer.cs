using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.Input;
using eLime.NetDaemonApps.Domain.Entities.TextSensors;
using Microsoft.Extensions.Logging;
using NetDaemon.HassModel.Entities;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.EnergyManager;

public class CarChargerEnergyConsumer : EnergyConsumer, IDynamicLoadConsumer
{
    public IDisposable? BalancingMethodChangedCommandHandler { get; set; }
    private readonly IScheduler _scheduler;

    public Int32 MinimumCurrent { get; set; }
    public Int32 MaximumCurrent { get; set; }
    public BalancingMethod BalancingMethod { get; set; }
    public Int32 OffCurrent { get; set; }

    public InputNumberEntity CurrentEntity { get; set; }
    public override bool Running => (CurrentEntity.State ?? 0) > OffCurrent;

    public override double PeakLoad => MinimumCurrentForConnectedCar * TotalVoltage;
    public TextSensor StateSensor { get; set; }

    public List<Car> Cars { get; set; }

    private Car? ConnectedCar => Cars.FirstOrDefault(x => x.IsConnectedToHomeCharger);
    public NumericEntity? VoltageEntity { get; set; }
    public Int32 SinglePhaseVoltage => Convert.ToInt32(VoltageEntity?.State ?? 230);

    private Int32 TotalVoltage => ConnectedCar?.Mode == CarChargingMode.Ac3Phase ? SinglePhaseVoltage * 3 : SinglePhaseVoltage;
    private Int32 MinimumCurrentForConnectedCar => ConnectedCar == null
        ? MinimumCurrent
        : ConnectedCar.MinimumCurrent < MinimumCurrent
            ? ConnectedCar.MinimumCurrent ?? MinimumCurrent
            : MinimumCurrent;

    public DateTimeOffset? _lastCurrentChange;

    public CarChargerEnergyConsumer(ILogger logger, String name, NumericEntity powerUsage, BinarySensor? criticallyNeeded, Double switchOnLoad, Double switchOffLoad, TimeSpan? minimumRuntime, TimeSpan? maximumRuntime, TimeSpan? minimumTimeout,
        TimeSpan? maximumTimeout, List<TimeWindow> timeWindows, Int32 minimumCurrent, Int32 maximumCurrent, Int32 offCurrent, InputNumberEntity currentEntity, NumericEntity voltageEntity, TextSensor stateSensor, List<Car> cars, IScheduler scheduler)
    {
        _scheduler = scheduler;
        SetCommonFields(logger, name, powerUsage, criticallyNeeded, switchOnLoad, switchOffLoad, minimumRuntime, maximumRuntime, minimumTimeout, maximumTimeout, timeWindows);
        MinimumCurrent = minimumCurrent;
        MaximumCurrent = maximumCurrent;
        OffCurrent = offCurrent;

        CurrentEntity = currentEntity;
        CurrentEntity.Changed += CurrentEntity_Changed;

        VoltageEntity = voltageEntity;
        StateSensor = stateSensor;
        Cars = cars;
    }

    public (Double current, Double netPowerChange) Rebalance(double netGridUsage, double peakUsage)
    {
        var currentChargerCurrent = CurrentEntity.State ?? 0;
        var currentCarCurrent = ConnectedCar?.CurrentEntity?.State ?? 0;

        var netGridCurrent = GetBalancingAdjustedGridCurrent(netGridUsage, peakUsage);

        double toBeChargerCurrent;
        double toBeCarCurrent = 0;

        if (ConnectedCar?.CanSetCurrent ?? false)
        {
            toBeCarCurrent = currentCarCurrent - netGridCurrent;
            toBeChargerCurrent = toBeCarCurrent;
        }
        else
        {
            toBeChargerCurrent = currentChargerCurrent - netGridCurrent;
        }

        var (chargerCurrent, chargerCurrentChanged) = SetChargerCurrent(ConnectedCar, toBeChargerCurrent, currentChargerCurrent);
        var (carCurrent, carCurrentChanged) = SetCarCurrentIfSupported(toBeCarCurrent, currentCarCurrent);

        if (!chargerCurrentChanged && !carCurrentChanged)
            return (0, 0);

        var netCurrentChange = ConnectedCar?.CanSetCurrent ?? false
            ? carCurrent - currentCarCurrent
            : chargerCurrent - currentChargerCurrent;

        return (ConnectedCar?.CanSetCurrent ?? false ? carCurrent : chargerCurrent, netCurrentChange * TotalVoltage);
    }

    private double GetBalancingAdjustedGridCurrent(double netGridUsage, double peakUsage)
    {
        return BalancingMethod switch
        {
            _ when CriticallyNeeded?.IsOn() == true => Math.Round((double)(netGridUsage - peakUsage) / TotalVoltage, 0, MidpointRounding.ToPositiveInfinity),
            BalancingMethod.NearPeak => Math.Round((double)(netGridUsage - peakUsage) / TotalVoltage, 0, MidpointRounding.ToPositiveInfinity),
            BalancingMethod.SolarPreferred => Math.Round((double)netGridUsage / TotalVoltage, 0, MidpointRounding.ToNegativeInfinity),
            _ => Math.Round((double)netGridUsage / TotalVoltage, 0, MidpointRounding.ToPositiveInfinity)
        };
    }

    private (double chargercurrent, bool changed) SetChargerCurrent(Car? connectedCar, double chargerCurrent, double currentCurrent)
    {
        if (chargerCurrent < MinimumCurrent)
            chargerCurrent = MinimumCurrent;

        if (chargerCurrent > MaximumCurrent)
            chargerCurrent = MaximumCurrent;

        if (connectedCar?.CanSetCurrent ?? false)
            chargerCurrent = MaximumCurrent;

        var netCurrentChange = chargerCurrent - currentCurrent;

        if (netCurrentChange == 0)
            return (chargerCurrent, false);

        ChangeCurrent(chargerCurrent);

        return (chargerCurrent, true);
    }

    private (double carCurrent, bool changed) SetCarCurrentIfSupported(double carCurrent, double currentCarCurrent)
    {
        if (!(ConnectedCar?.CanSetCurrent ?? false))
            return (carCurrent, false);

        if (carCurrent < ConnectedCar.MinimumCurrent)
            carCurrent = ConnectedCar.MinimumCurrent ?? 0;

        if (carCurrent > ConnectedCar.MaximumCurrent)
            carCurrent = ConnectedCar.MaximumCurrent ?? 0;

        var netCurrentChange = carCurrent - currentCarCurrent;

        if (netCurrentChange == 0)
            return (carCurrent, false);

        ConnectedCar.ChangeCurrent(carCurrent);
        return (carCurrent, true);
    }

    private void ChangeCurrent(Double toBeCurrent)
    {
        if (_lastCurrentChange?.Add(TimeSpan.FromSeconds(5)) > _scheduler.Now)
            return;

        CurrentEntity.Change(toBeCurrent);
        _lastCurrentChange = _scheduler.Now;
    }


    protected override EnergyConsumerState GetDesiredState(DateTimeOffset? now)
    {
        var connectedCarNeedsEnergy = ConnectedCar?.NeedsEnergy ?? false;
        var needsEnergy = (StateSensor.State == CarChargerStates.Occupied.ToString() || StateSensor.State == CarChargerStates.Charging.ToString()) && connectedCarNeedsEnergy;

        return Running switch
        {
            true when !needsEnergy => EnergyConsumerState.Off,
            true => EnergyConsumerState.Running,
            false when needsEnergy && CriticallyNeeded != null && CriticallyNeeded.IsOn() => EnergyConsumerState.CriticallyNeedsEnergy,
            false when needsEnergy => EnergyConsumerState.NeedsEnergy,
            false => EnergyConsumerState.Off,
        };
    }

    public override bool CanStart(DateTimeOffset now)
    {
        if (State is EnergyConsumerState.Running or EnergyConsumerState.Off)
            return false;

        if (HasTimeWindow() && !IsWithinTimeWindow(now))
            return false;

        if (MinimumTimeout == null)
            return true;

        return !(LastRun?.Add(MinimumTimeout.Value) > now);
    }

    public override bool CanForceStop(DateTimeOffset now)
    {
        if (MinimumRuntime != null && StartedAt?.Add(MinimumRuntime.Value) > now)
            return false;

        if (CriticallyNeeded != null && CriticallyNeeded.IsOn())
            return false;

        if (BalancingMethod == BalancingMethod.NearPeak)
            return false;

        return true;
    }

    public override bool CanForceStopOnPeakLoad(DateTimeOffset now)
    {
        if (MinimumRuntime != null && StartedAt?.Add(MinimumRuntime.Value) > now)
            return false;

        return true;
    }


    public override void TurnOn()
    {
        if (ConnectedCar?.CanSetCurrent ?? false)
        {
            ChangeCurrent(MaximumCurrent);
            ConnectedCar?.ChangeCurrent(ConnectedCar?.MinimumCurrent ?? 1);
        }
        else
        {
            ChangeCurrent(MinimumCurrent);
        }
    }

    public override void TurnOff()
    {
        ChangeCurrent(OffCurrent);
    }

    public new void Dispose()
    {
        base.Dispose();
        BalancingMethodChangedCommandHandler?.Dispose();
        StateSensor.Dispose();

        CurrentEntity.Changed -= CurrentEntity_Changed;
        CurrentEntity.Dispose();

        foreach (var car in Cars)
            car.Dispose();
    }

    private void CurrentEntity_Changed(object? sender, InputNumberSensorEventArgs e)
    {
        if (e.New.State >= MinimumCurrent)
        {
            if (State != EnergyConsumerState.Running)
                CheckDesiredState(new EnergyConsumerStartedEvent(this, EnergyConsumerState.Running));
        }
        else
            CheckDesiredState(new EnergyConsumerStoppedEvent(this, EnergyConsumerState.Off));
    }
}