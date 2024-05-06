using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.DeviceTracker;
using eLime.NetDaemonApps.Domain.Entities.Input;
using NetDaemon.HassModel.Entities;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.EnergyManager;

public class Car
{
    private readonly IScheduler _scheduler;
    public String Name { get; }
    public CarChargingMode Mode { get; }

    public BinarySwitch? ChargerSwitch { get; }

    public InputNumberEntity? CurrentEntity { get; set; }
    public int? MinimumCurrent { get; }
    public int? MaximumCurrent { get; }

    public Double BatteryCapacity { get; }

    public NumericEntity BatteryPercentageSensor { get; }
    public NumericEntity? MaxBatteryPercentageSensor { get; }
    public Boolean RemainOnAtFullBattery { get; }
    public BinarySensor CableConnectedSensor { get; }
    public DeviceTracker Location { get; }

    public DateTimeOffset? _lastCurrentChange;

    public Car(string name, CarChargingMode mode, BinarySwitch? chargerSwitch, InputNumberEntity? currentEntity, int? minimumCurrent, int? maximumCurrent,
        double batteryCapacity, NumericEntity batteryPercentageSensor, NumericEntity? maxBatteryPercentageSensor, bool remainOnAtFullBattery,
        BinarySensor cableConnectedSensor, DeviceTracker location, IScheduler scheduler)
    {
        _scheduler = scheduler;
        Name = name;
        Mode = mode;

        if (chargerSwitch != null)
        {
            ChargerSwitch = chargerSwitch;
            ChargerSwitch.TurnedOn += ChargerSwitchTurnedOn;
            ChargerSwitch.TurnedOff += ChargerSwitchTurnedOff;
        }

        CurrentEntity = currentEntity;
        MinimumCurrent = minimumCurrent;
        MaximumCurrent = maximumCurrent;

        BatteryCapacity = batteryCapacity;
        BatteryPercentageSensor = batteryPercentageSensor;
        MaxBatteryPercentageSensor = maxBatteryPercentageSensor;
        RemainOnAtFullBattery = remainOnAtFullBattery;

        CableConnectedSensor = cableConnectedSensor;
        Location = location;
    }

    public event EventHandler<BinarySensorEventArgs>? ChargerTurnedOn;
    public event EventHandler<BinarySensorEventArgs>? ChargerTurnedOff;

    protected void OnChargerSwitchTurnedOn(BinarySensorEventArgs e)
    {
        ChargerTurnedOn?.Invoke(this, e);
    }
    protected void OnChargerSwitchTurnedOff(BinarySensorEventArgs e)
    {
        ChargerTurnedOff?.Invoke(this, e);
    }

    private void ChargerSwitchTurnedOn(object? sender, BinarySensorEventArgs e)
    {
        OnChargerSwitchTurnedOn(e);
    }

    private void ChargerSwitchTurnedOff(object? sender, BinarySensorEventArgs e)
    {
        OnChargerSwitchTurnedOff(e);
    }

    public Boolean IsConnectedToHomeCharger => CableConnectedSensor.IsOn() && Location.State == "home";

    public Boolean CanSetCurrent => IsConnectedToHomeCharger && CurrentEntity != null;

    public Boolean NeedsEnergy => RemainOnAtFullBattery ||
        (MaxBatteryPercentageSensor != null
            ? BatteryPercentageSensor.State < MaxBatteryPercentageSensor.State
            : BatteryPercentageSensor.State < 100
        );

    public Boolean IsRunning => ChargerSwitch == null
        ? CanSetCurrent
            ? CurrentEntity.State >= MinimumCurrent
            : true
        : CanSetCurrent
          ? ChargerSwitch.IsOn() && CurrentEntity.State >= MinimumCurrent
          : ChargerSwitch.IsOn();

    public void TurnOnCharger()
    {
        ChargerSwitch?.TurnOn();
    }
    public void TurnOffCharger()
    {
        ChargerSwitch?.TurnOff();
    }

    public void ChangeCurrent(Double toBeCurrent)
    {
        if (CurrentEntity == null)
            return;

        if (_lastCurrentChange?.Add(TimeSpan.FromSeconds(5)) > _scheduler.Now)
            return;

        CurrentEntity.Change(toBeCurrent);
        _lastCurrentChange = _scheduler.Now;
    }

    public void Dispose()
    {
        CurrentEntity?.Dispose();

        if (ChargerSwitch == null) return;
        ChargerSwitch.TurnedOn -= ChargerSwitchTurnedOn;
        ChargerSwitch.TurnedOff -= ChargerSwitchTurnedOff;
        ChargerSwitch.Dispose();
    }


}

public enum CarChargingMode
{
    Ac1Phase,
    Ac3Phase
}