using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.SmartIrrigation;

public class ContainerIrrigationZone : IrrigationZone
{
    public NumericSensor VolumeSensor { get; }
    public BinarySensor OverflowSensor { get; }
    public Int32 TargetVolume { get; }
    public Int32 CriticallyLowVolume { get; }
    public Int32 LowVolume { get; }

    public ContainerIrrigationZone(String name, Int32 flowRate, BinarySwitch valve, NumericSensor volumeSensor, BinarySensor overflowSensor, Int32 criticallyLowVolume, Int32 lowVolume, Int32 targetVolume)
    {
        SetCommonFields(name, flowRate, valve);

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
        if (OverflowSensor.IsOn())
            return NeedsWatering.No;

        if (CurrentlyWatering)
            return VolumeSensor.State >= TargetVolume
                ? NeedsWatering.No
                : NeedsWatering.Ongoing;

        return VolumeSensor.State <= CriticallyLowVolume
            ? NeedsWatering.Critical
            : VolumeSensor.State <= LowVolume
                ? NeedsWatering.Yes
                : NeedsWatering.No;
    }

    public override bool CanStartWatering(DateTimeOffset now)
    {
        if (State is NeedsWatering.Ongoing or NeedsWatering.No)
            return false;

        return OverflowSensor.IsOff();
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