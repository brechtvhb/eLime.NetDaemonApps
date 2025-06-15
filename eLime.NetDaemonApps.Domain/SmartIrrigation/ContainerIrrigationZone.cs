using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SmartIrrigation;

public class ContainerIrrigationZone : IrrigationZone
{
    public NumericSensor VolumeSensor { get; }
    public BinarySensor OverflowSensor { get; }
    public int TargetVolume { get; }
    public int CriticallyLowVolume { get; }
    public int LowVolume { get; }

    public ContainerIrrigationZone(ILogger logger, string name, int flowRate, BinarySwitch valve, NumericSensor volumeSensor, BinarySensor overflowSensor, int criticallyLowVolume, int lowVolume, int targetVolume, IScheduler scheduler, DateTimeOffset? irrigationSeasonStart, DateTimeOffset? irrigationSeasonEnd)
    {
        SetCommonFields(logger, name, flowRate, valve, scheduler, irrigationSeasonStart, irrigationSeasonEnd);

        VolumeSensor = volumeSensor;
        VolumeSensor.Changed += CheckDesiredState;

        OverflowSensor = overflowSensor;
        OverflowSensor.TurnedOn += CheckDesiredState;
        OverflowSensor.TurnedOff += CheckDesiredState;

        TargetVolume = targetVolume;
        CriticallyLowVolume = criticallyLowVolume;
        LowVolume = lowVolume;
    }

    private void CheckDesiredState(object? o, NumericSensorEventArgs sender)
    {
        CheckDesiredState();
    }

    private void CheckDesiredState(object? o, BinarySensorEventArgs sender)
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