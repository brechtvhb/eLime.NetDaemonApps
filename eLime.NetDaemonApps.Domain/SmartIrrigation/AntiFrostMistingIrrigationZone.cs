using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SmartIrrigation;

public class AntiFrostMistingIrrigationZone : IrrigationZone, IZoneWithLimitedRuntime
{
    public NumericSensor TemperatureSensor { get; }
    public Double CriticallyLowTemperature { get; }
    public Double LowTemperature { get; }
    public TimeSpan MistingDuration { get; }
    public TimeSpan MistingTimeout { get; }


    public AntiFrostMistingIrrigationZone(ILogger logger, String name, Int32 flowRate, BinarySwitch valve, NumericSensor temperatureSensor, Double criticallyLowTemperature, Double lowTemperature, TimeSpan mistingDuration, TimeSpan mistingTimeout, IScheduler scheduler, DateTimeOffset? irrigationSeasonStart, DateTimeOffset? irrigationSeasonEnd)
    {
        SetCommonFields(logger, name, flowRate, valve, scheduler, irrigationSeasonStart, irrigationSeasonEnd);

        TemperatureSensor = temperatureSensor;
        TemperatureSensor.Changed += CheckDesiredState;

        CriticallyLowTemperature = criticallyLowTemperature;
        LowTemperature = lowTemperature;
        MistingDuration = mistingDuration;
        MistingTimeout = mistingTimeout;
    }

    private void CheckDesiredState(Object? o, NumericSensorEventArgs sender)
    {
        CheckDesiredState();
    }

    protected override NeedsWatering GetDesiredState()
    {
        AdjustYearIfNeeded();

        if (HasIrrigationSeason && !WithinIrrigationSeason)
            return NeedsWatering.No;

        if (CurrentlyWatering)
            return NeedsWatering.Ongoing;

        return TemperatureSensor.State <= CriticallyLowTemperature
            ? NeedsWatering.Critical
            : TemperatureSensor.State <= LowTemperature
                ? NeedsWatering.Yes
                : NeedsWatering.No;
    }


    public override bool CanStartWatering(DateTimeOffset now, bool energyAvailable)
    {
        if (Mode == ZoneMode.EnergyManaged && !energyAvailable)
            return false;

        if (State is NeedsWatering.Ongoing or NeedsWatering.No)
            return false;

        if (LastWatering == null)
            return true;

        return !(LastWatering?.Add(MistingTimeout) > now);
    }

    public TimeSpan? GetRunTime(ILogger logger, DateTimeOffset now)
    {
        return GetRemainingRunTime(MistingDuration, now);
    }

    public override bool CheckForForceStop(DateTimeOffset now)
    {
        if (Mode == ZoneMode.Manual)
            return false;

        if (WateringStartedAt?.Add(MistingDuration) < now)
            return true;

        if (State == NeedsWatering.No & Valve.IsOn())
            return true;

        return false;
    }
    public new void Dispose()
    {
        TemperatureSensor.Changed -= CheckDesiredState;
        TemperatureSensor.Dispose();

        base.Dispose();
    }
}