using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.Input;
using eLime.NetDaemonApps.Domain.Entities.TextSensors;
using Microsoft.Extensions.Logging;
using NetDaemon.HassModel.Entities;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.EnergyManager;

public class CarChargerEnergyConsumer : EnergyConsumer, IDynamicLoadConsumer
{
    private readonly IScheduler _scheduler;
    public Int32 MinimumCurrent { get; set; }
    public Int32 MaximumCurrent { get; set; }
    public Int32 OffCurrent { get; set; }

    public InputNumberEntity CurrentEntity { get; set; }
    public override bool Running => (CurrentEntity.State ?? 0) > OffCurrent;

    public override double PeakLoad => (CurrentEntity.State > OffCurrent) ? MinimumCurrentForConnectedCar * VoltageMultiplier : 0;
    public TextSensor StateSensor { get; set; }

    public List<Car> Cars { get; set; }

    private Car? ConnectedCar => Cars.SingleOrDefault(x => x.IsConnectedToHomeCharger);
    private Int32 VoltageMultiplier => ConnectedCar?.Mode == CarChargingMode.Ac3Phase ? 230 * 3 : 230;
    private Int32 MinimumCurrentForConnectedCar => ConnectedCar == null
        ? MinimumCurrent
        : ConnectedCar.MinimumCurrent < MinimumCurrent
            ? ConnectedCar.MinimumCurrent ?? MinimumCurrent
            : MinimumCurrent;

    public DateTimeOffset? _lastCurrentChange;

    public CarChargerEnergyConsumer(ILogger logger, String name, NumericEntity powerUsage, BinarySensor? criticallyNeeded, Double switchOnLoad, Double switchOffLoad, TimeSpan? minimumRuntime, TimeSpan? maximumRuntime, TimeSpan? minimumTimeout,
        TimeSpan? maximumTimeout, List<TimeWindow> timeWindows, Int32 minimumCurrent, Int32 maximumCurrent, Int32 offCurrent, InputNumberEntity currentEntity, TextSensor stateSensor, List<Car> cars, IScheduler scheduler)
    {
        _scheduler = scheduler;
        SetCommonFields(logger, name, powerUsage, criticallyNeeded, switchOnLoad, switchOffLoad, minimumRuntime, maximumRuntime, minimumTimeout, maximumTimeout, timeWindows);
        MinimumCurrent = minimumCurrent;
        MaximumCurrent = maximumCurrent;
        OffCurrent = offCurrent;

        CurrentEntity = currentEntity;
        CurrentEntity.Changed += CurrentEntity_Changed;
        StateSensor = stateSensor;
        Cars = cars;
    }

    //TODO: needs adjustment for cars where you can set the current
    public (Double current, Double netPowerChange) Rebalance(double netGridUsage)
    {
        var currentCurrent = CurrentEntity.State ?? 0;
        var netGridCurrent = Math.Round((double)netGridUsage / VoltageMultiplier, 0, MidpointRounding.ToZero);

        var toBeCurrent = currentCurrent - netGridCurrent;

        if (toBeCurrent < MinimumCurrent)
            toBeCurrent = MinimumCurrent;

        if (toBeCurrent > MaximumCurrent)
            toBeCurrent = MaximumCurrent;

        var netCurrentChange = toBeCurrent - currentCurrent;

        if (netCurrentChange == 0)
            return (0, 0);

        ChangeCurrent(toBeCurrent);
        return (toBeCurrent, netCurrentChange * VoltageMultiplier);
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
        var connectedCarNeedsEnergy = ConnectedCar != null && (ConnectedCar.NeedsEnergy || (ConnectedCar.IgnoreStateOnForceCharge && CriticallyNeeded != null && CriticallyNeeded.IsOn()));
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
        ChangeCurrent(MinimumCurrent);
    }

    public override void TurnOff()
    {
        ChangeCurrent(OffCurrent);
    }

    public new void Dispose()
    {
        base.Dispose();
        StateSensor.Dispose();

        CurrentEntity.Changed -= CurrentEntity_Changed;
        CurrentEntity.Dispose();
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