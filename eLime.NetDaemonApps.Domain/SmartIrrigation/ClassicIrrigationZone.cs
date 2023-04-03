using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;

namespace eLime.NetDaemonApps.Domain.SmartIrrigation;

public class ClassicIrrigationZone : IrrigationZone, IZoneWithLimitedRuntime
{
    public NumericSensor SoilMoistureSensor { get; }
    public Int32 CriticallyLowSoilMoisture { get; }
    public Int32 LowSoilMoisture { get; }
    public Int32 TargetSoilMoisture { get; }
    public TimeSpan? MaxDuration { get; }
    public TimeSpan? MinimumTimeout { get; }
    public TimeOnly? IrrigationStartWindow { get; }
    public TimeOnly? IrrigationEndWindow { get; }


    public ClassicIrrigationZone(String name, Int32 flowRate, BinarySwitch valve, NumericSensor soilMoistureSensor, Int32 criticallyLowSoilMoisture, Int32 lowSoilMoisture, Int32 targetSoilMoisture, TimeSpan? maxDuration, TimeSpan? minimumTimeout, TimeOnly? irrigationStartWindow, TimeOnly? irrigationEndWindow)
    {
        SetCommonFields(name, flowRate, valve);

        SoilMoistureSensor = soilMoistureSensor;
        SoilMoistureSensor.Changed += CheckDesiredState;


        CriticallyLowSoilMoisture = criticallyLowSoilMoisture;
        LowSoilMoisture = lowSoilMoisture;
        TargetSoilMoisture = targetSoilMoisture;
        MaxDuration = maxDuration;
        MinimumTimeout = minimumTimeout;
        IrrigationStartWindow = irrigationStartWindow;
        IrrigationEndWindow = irrigationEndWindow;
    }

    private void CheckDesiredState(Object? o, NumericSensorEventArgs sender)
    {
        CheckDesiredState();
    }

    protected override NeedsWatering GetDesiredState()
    {
        if (CurrentlyWatering)
            return SoilMoistureSensor.State >= TargetSoilMoisture
                ? NeedsWatering.No
                : NeedsWatering.Ongoing;

        return SoilMoistureSensor.State <= CriticallyLowSoilMoisture
            ? NeedsWatering.Critical
            : SoilMoistureSensor.State <= LowSoilMoisture
                ? NeedsWatering.Yes
                : NeedsWatering.No;
    }

    public override bool CanStartWatering(DateTimeOffset now, bool energyAvailable)
    {
        if (Mode == ZoneMode.EnergyManaged && !energyAvailable)
            return false;

        if (State is NeedsWatering.Ongoing or NeedsWatering.No)
            return false;

        if (!CheckIfWithinWindow(now)) return false;

        if (MinimumTimeout == null || LastWatering == null)
            return true;

        return !(LastWatering?.Add(MinimumTimeout.Value) > now);
    }

    public TimeSpan? GetRunTime(DateTimeOffset now)
    {

        if (IrrigationEndWindow == null)
            return GetRemainingRrunTime(MaxDuration, now);

        var endWindow = now.Add(-now.TimeOfDay).Add(IrrigationEndWindow.Value.ToTimeSpan());
        if (now > endWindow)
            endWindow = endWindow.AddDays(1);

        var timeUntilEndOfWindow = now - endWindow;

        if (MaxDuration == null)
            return GetRemainingRrunTime(timeUntilEndOfWindow, now);

        return MaxDuration < timeUntilEndOfWindow
            ? GetRemainingRrunTime(MaxDuration, now)
            : GetRemainingRrunTime(timeUntilEndOfWindow, now);
    }

    public bool CheckIfWithinWindow(DateTimeOffset now)
    {
        if (IrrigationStartWindow == null && IrrigationEndWindow == null)
            return true;

        if (IrrigationStartWindow != null && IrrigationEndWindow == null)
        {
            var startWindow = now.Add(-now.TimeOfDay).Add(IrrigationStartWindow.Value.ToTimeSpan());
            if (startWindow > now)
                return false;
        }
        else if (IrrigationStartWindow == null && IrrigationEndWindow != null)
        {
            var endWindow = now.Add(-now.TimeOfDay).Add(IrrigationEndWindow.Value.ToTimeSpan());
            if (endWindow < now)
                return false;
        }
        else if (IrrigationStartWindow != null && IrrigationEndWindow != null)
        {
            var startWindow = now.Add(-now.TimeOfDay).Add(IrrigationStartWindow.Value.ToTimeSpan());
            var endWindow = now.Add(-now.TimeOfDay).Add(IrrigationEndWindow.Value.ToTimeSpan());

            if (IrrigationStartWindow > IrrigationEndWindow)
            {
                if (now.TimeOfDay > IrrigationStartWindow.Value.ToTimeSpan())
                    endWindow = endWindow.AddDays(1);

                if (now.TimeOfDay < IrrigationEndWindow.Value.ToTimeSpan())
                    startWindow = startWindow.AddDays(1);
            }

            if (startWindow > now || endWindow < now)
                return false;
        }

        return true;
    }

    public override bool CheckForForceStop(DateTimeOffset now)
    {
        if (MaxDuration != null && WateringStartedAt?.Add(MaxDuration.Value) < now)
            return true;

        if (!CheckIfWithinWindow(now))
            return true;

        if (State == NeedsWatering.No & Valve.IsOn())
            return true;

        return false;
    }

    public new void Dispose()
    {
        SoilMoistureSensor.Changed -= CheckDesiredState;
        SoilMoistureSensor.Dispose();

        base.Dispose();
    }
}
