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
    public IDisposable? BalanceOnBehalfOfChangedCommandHandler { get; set; }
    private readonly IScheduler _scheduler;

    public Int32 MinimumCurrent { get; set; }
    public Int32 MaximumCurrent { get; set; }
    public BalancingMethod BalancingMethod { get; private set; }
    public BalanceOnBehalfOf BalanceOnBehalfOf { get; private set; }

    public double ReleasablePowerWhenBalancingOnBehalfOf => CurrentLoad - (MinimumCurrentForConnectedCar * TotalVoltage);
    public TimeSpan MinimumRebalancingInterval => TimeSpan.FromSeconds(20); //TODO: config setting
    private DateTimeOffset? _balancingMethodLastChangedAt;

    public Int32 OffCurrent { get; set; }

    public InputNumberEntity CurrentEntity { get; set; }

    public override bool Running => (ConnectedCar?.IsRunning ?? false) && (CurrentEntity.State ?? 0) > OffCurrent;

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

    private Int32 CurrentCurrentForConnectedCar => CurrentLoad > 0 ? Convert.ToInt32(CurrentLoad / TotalVoltage) : 0;

    public DateTimeOffset? _lastCurrentChange;

    public CarChargerEnergyConsumer(ILogger logger, String name, NumericEntity powerUsage, BinarySensor? criticallyNeeded, Double switchOnLoad, Double switchOffLoad, TimeSpan? minimumRuntime, TimeSpan? maximumRuntime, TimeSpan? minimumTimeout,
        TimeSpan? maximumTimeout, List<TimeWindow> timeWindows, String timezone, Int32 minimumCurrent, Int32 maximumCurrent, Int32 offCurrent, InputNumberEntity currentEntity, NumericEntity voltageEntity, TextSensor stateSensor, List<Car> cars, IScheduler scheduler)
    {
        _scheduler = scheduler;
        SetCommonFields(logger, name, powerUsage, criticallyNeeded, switchOnLoad, switchOffLoad, minimumRuntime, maximumRuntime, minimumTimeout, maximumTimeout, timeWindows, timezone);
        MinimumCurrent = minimumCurrent;
        MaximumCurrent = maximumCurrent;
        OffCurrent = offCurrent;

        CurrentEntity = currentEntity;
        CurrentEntity.Changed += CurrentEntity_Changed;

        VoltageEntity = voltageEntity;
        StateSensor = stateSensor;
        Cars = cars;

        foreach (var car in Cars)
        {
            if (car.ChargerSwitch == null)
                continue;

            car.ChargerTurnedOn += Car_ChargerTurnedOn;
            car.ChargerTurnedOff += Car_ChargerTurnedOff;
            car.CarConnected += Car_CarConnected;
        }
    }


    public void SetBalancingMethod(DateTimeOffset now, BalancingMethod balancingMethod)
    {
        BalancingMethod = balancingMethod;
        _balancingMethodLastChangedAt = now;
    }
    public void SetBalanceOnBehalfOf(BalanceOnBehalfOf balanceOnBehalfOf)
    {
        BalanceOnBehalfOf = balanceOnBehalfOf;
    }

    public (Double current, Double netPowerChange) Rebalance(double netGridUsage, double trailingNetGridUsage, double peakUsage)
    {
        if (_lastCurrentChange?.Add(MinimumRebalancingInterval) > _scheduler.Now)
            return (0, 0);

        var currentChargerCurrent = CurrentEntity.State ?? 0;

        var netGridCurrent = GetBalancingAdjustedGridCurrent(netGridUsage, trailingNetGridUsage, peakUsage);

        double toBeChargerCurrent;
        double toBeCarCurrent = 0;

        if (ConnectedCar?.CanSetCurrent ?? false)
        {
            toBeCarCurrent = CurrentCurrentForConnectedCar - netGridCurrent;
            toBeChargerCurrent = toBeCarCurrent;
        }
        else
        {
            toBeChargerCurrent = currentChargerCurrent - netGridCurrent;
        }

        var (chargerCurrent, chargerCurrentChanged) = SetChargerCurrent(ConnectedCar, toBeChargerCurrent, currentChargerCurrent);
        var (carCurrent, carCurrentChanged) = SetCarCurrentIfSupported(toBeCarCurrent, CurrentCurrentForConnectedCar);

        if (!chargerCurrentChanged && !carCurrentChanged)
            return (0, 0);

        var netCurrentChange = ConnectedCar?.CanSetCurrent ?? false
            ? carCurrent - CurrentCurrentForConnectedCar
            : chargerCurrent - currentChargerCurrent;

        return (ConnectedCar?.CanSetCurrent ?? false ? carCurrent : chargerCurrent, netCurrentChange * TotalVoltage);
    }

    private double GetBalancingAdjustedGridCurrent(double netGridUsage, double trailingNetGridUsage, double peakUsage)
    {
        var daKleinBeetjeMagJeNegeren = TotalVoltage * 0.1;
        var midPoint = TotalVoltage * 0.5;

        return BalancingMethod switch
        {
            _ when CriticallyNeeded?.IsOn() == true => Math.Round((netGridUsage - peakUsage) / TotalVoltage, 0, MidpointRounding.ToPositiveInfinity),
            BalancingMethod.NearPeak => Math.Round((netGridUsage - peakUsage) / TotalVoltage, 0, MidpointRounding.ToPositiveInfinity),
            BalancingMethod.SolarPreferred => Math.Round((netGridUsage + daKleinBeetjeMagJeNegeren) / TotalVoltage, 0, MidpointRounding.ToNegativeInfinity),
            BalancingMethod.MidPoint => Math.Round((trailingNetGridUsage - midPoint) / TotalVoltage, 0, MidpointRounding.ToPositiveInfinity),
            _ => Math.Round((netGridUsage - daKleinBeetjeMagJeNegeren) / TotalVoltage, 0, MidpointRounding.ToPositiveInfinity)
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
        _lastCurrentChange = _scheduler.Now;

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

        //Turns of entire charging station if no known car is connected that needs energy.
        if (!Running && !needsEnergy && (CurrentEntity.State ?? 0) > OffCurrent)
            TurnOff();

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

        if (CriticallyNeeded?.EntityState?.LastUpdated?.AddMinutes(3) > now)
            return false;

        if (BalancingMethod == BalancingMethod.NearPeak)
            return false;

        if (_balancingMethodLastChangedAt?.AddMinutes(3) > now)
            return false;

        if (_lastCurrentChange?.AddMinutes(3) > now || ConnectedCar?.LastCurrentChange?.AddMinutes(3) > now)
            return false;

        if (CurrentCurrentForConnectedCar > MinimumCurrentForConnectedCar)
            return false;

        return true;
    }

    public override bool CanForceStopOnPeakLoad(DateTimeOffset now)
    {
        if (MinimumRuntime != null && StartedAt?.Add(MinimumRuntime.Value) > now)
            return false;

        //Can re-balance
        if (CurrentCurrentForConnectedCar > MinimumCurrentForConnectedCar)
            return false;

        return true;
    }


    public override void TurnOn()
    {
        if (ConnectedCar?.ChargerSwitch != null)
            ConnectedCar.TurnOnCharger();

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
        if (ConnectedCar?.ChargerSwitch != null)
        {
            ConnectedCar.TurnOffCharger();
        }
        else
        {
            ChangeCurrent(OffCurrent);
        }
    }

    public override void DisposeInternal()
    {
        BalancingMethodChangedCommandHandler?.Dispose();
        BalanceOnBehalfOfChangedCommandHandler?.Dispose();

        CurrentEntity.Changed -= CurrentEntity_Changed;
        CurrentEntity.Dispose();

        StateSensor.Dispose();

        foreach (var car in Cars)
        {
            if (car.ChargerSwitch != null)
            {
                car.ChargerTurnedOn -= Car_ChargerTurnedOn;
                car.ChargerTurnedOff -= Car_ChargerTurnedOff;
                car.CarConnected -= Car_CarConnected;
            }

            car.Dispose();
        }
    }

    //Needed for when manually changing dropdown in home assistant
    private void CurrentEntity_Changed(object? sender, InputNumberSensorEventArgs e)
    {
        if (e.New.State < MinimumCurrent)
        {
            if (State == EnergyConsumerState.Running)
                CheckDesiredState(new EnergyConsumerStoppedEvent(this, EnergyConsumerState.Off));

            return;
        }

        if (ConnectedCar?.ChargerSwitch == null)
        {
            if (State != EnergyConsumerState.Running)
                CheckDesiredState(new EnergyConsumerStartedEvent(this, EnergyConsumerState.Running));

            return;
        }

        if (State != EnergyConsumerState.Running && ConnectedCar.ChargerSwitch.IsOn())
            CheckDesiredState(new EnergyConsumerStartedEvent(this, EnergyConsumerState.Running));
    }

    private void Car_ChargerTurnedOn(object? sender, BinarySensorEventArgs e)
    {
        if (State != EnergyConsumerState.Running && CurrentEntity.State >= MinimumCurrent)
            CheckDesiredState(new EnergyConsumerStartedEvent(this, EnergyConsumerState.Running));
    }
    private void Car_ChargerTurnedOff(object? sender, BinarySensorEventArgs e)
    {
        if (State == EnergyConsumerState.Running)
            CheckDesiredState(new EnergyConsumerStoppedEvent(this, EnergyConsumerState.Off));
    }

    private void Car_CarConnected(object? sender, BinarySensorEventArgs e)
    {
        Logger.LogInformation($"Car '{ConnectedCar?.Name}' connected.");

        if (ConnectedCar?.AutoPowerOnWhenConnecting ?? false)
            TurnOn();
    }

}