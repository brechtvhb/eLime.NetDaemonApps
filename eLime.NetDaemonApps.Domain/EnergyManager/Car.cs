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

    public int? MinimumCurrent { get; }
    public int? MaximumCurrent { get; }
    public InputNumberEntity? CurrentEntity { get; set; }

    public Double BatteryCapacity { get; }

    public NumericEntity BatteryPercentageSensor { get; }
    public NumericEntity? MaxBatteryPercentageSensor { get; }
    public BinarySensor CableConnectedSensor { get; }
    public DeviceTracker Location { get; }

    public DateTimeOffset? _lastCurrentChange;

    public Car(string name, CarChargingMode mode, InputNumberEntity? currentEntity, int? minimumCurrent, int? maximumCurrent,
        double batteryCapacity, NumericEntity batteryPercentageSensor, NumericEntity? maxBatteryPercentageSensor,
        BinarySensor cableConnectedSensor, DeviceTracker location, IScheduler scheduler)
    {
        _scheduler = scheduler;
        Name = name;
        Mode = mode;

        CurrentEntity = currentEntity;
        MinimumCurrent = minimumCurrent;
        MaximumCurrent = maximumCurrent;

        BatteryCapacity = batteryCapacity;
        BatteryPercentageSensor = batteryPercentageSensor;
        MaxBatteryPercentageSensor = maxBatteryPercentageSensor;
        CableConnectedSensor = cableConnectedSensor;
        Location = location;
    }

    public Boolean IsConnectedToHomeCharger => CableConnectedSensor.IsOn() && Location.State == "home";
    public Boolean CanSetCurrent => IsConnectedToHomeCharger && CurrentEntity != null;

    public Boolean NeedsEnergy => MaxBatteryPercentageSensor != null
        ? BatteryPercentageSensor.State < MaxBatteryPercentageSensor.State
        : BatteryPercentageSensor.State < 100;

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
    }


}

public enum CarChargingMode
{
    Ac1Phase,
    Ac3Phase
}