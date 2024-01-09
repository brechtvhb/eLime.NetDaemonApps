using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SmartIrrigation;

public class ContainerIrrigationZone : IrrigationZone
{
    public NumericSensor VolumeSensor { get; }
    public BinarySensor OverflowSensor { get; }
    public Int32 TargetVolume { get; }
    public Int32 CriticallyLowVolume { get; }
    public Int32 LowVolume { get; }

    public ContainerIrrigationZone(String name, Int32 flowRate, BinarySwitch valve, NumericSensor volumeSensor, BinarySensor overflowSensor, Int32 criticallyLowVolume, Int32 lowVolume, Int32 targetVolume, IScheduler scheduler, DateTimeOffset? irrigationSeasonStart, DateTimeOffset? irrigationSeasonEnd)
    {
        SetCommonFields(name, flowRate, valve, scheduler, irrigationSeasonStart, irrigationSeasonEnd);

        VolumeSensor = volumeSensor;
        VolumeSensor.Changed += CheckDesiredState;

        OverflowSensor = overflowSensor;
        OverflowSensor.TurnedOn += CheckDesiredState;
        OverflowSensor.TurnedOff += CheckDesiredState;

        TargetVolume = targetVolume;
        CriticallyLowVolume = criticallyLowVolume;
        LowVolume = lowVolume;
    }

    private void CheckDesiredState(Object? o, NumericSensorEventArgs sender)
    {
        CheckDesiredState();
    }

    private void CheckDesiredState(Object? o, BinarySensorEventArgs sender)
    {
        CheckDesiredState();
    }

    protected override NeedsWatering GetDesiredState()
    {
        AdjustYearIfNeeded();

        if (HasIrrigationSeason && !WithinIrrigationSeason)
            return NeedsWatering.No;

        if (OverflowSensor.IsOn())
            return NeedsWatering.No;

        return CurrentlyWatering switch
        {
            true when Mode == ZoneMode.Manual => NeedsWatering.Ongoing,
            true => VolumeSensor.State >= TargetVolume ? NeedsWatering.No : NeedsWatering.Ongoing,
            _ => VolumeSensor.State <= CriticallyLowVolume
                ? NeedsWatering.Critical
                : VolumeSensor.State <= LowVolume
                    ? NeedsWatering.Yes
                    : NeedsWatering.No
        };
    }

    public override bool CanStartWatering(DateTimeOffset now, bool energyAvailable)
    {
        if (Mode == ZoneMode.EnergyManaged && !energyAvailable)
            return false;

        if (State is NeedsWatering.Ongoing or NeedsWatering.No)
            return false;

        return OverflowSensor.IsOff();
    }

    public override bool CheckForForceStop(DateTimeOffset now)
    {
        if (OverflowSensor.IsOn())
            return true;

        if (State == NeedsWatering.No & Valve.IsOn())
            return true;

        return false;
    }

    public new void Dispose()
    {
        VolumeSensor.Changed -= CheckDesiredState;
        VolumeSensor.Dispose();

        OverflowSensor.TurnedOn -= CheckDesiredState;
        OverflowSensor.TurnedOff -= CheckDesiredState;
        OverflowSensor.Dispose();

        base.Dispose();
    }
}