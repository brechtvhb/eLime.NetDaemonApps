using eLime.NetDaemonApps.Domain.Entities.Input;
using Microsoft.Extensions.Logging;
using NetDaemon.HassModel.Entities;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.EnergyManager;

public class Battery : IDisposable
{
    protected ILogger Logger;
    protected IScheduler Scheduler;

    public String Name { get; private set; }
    public decimal Capacity { get; private set; }
    public int MaxChargePower { get; private set; }
    public int MaxDischargePower { get; private set; }

    public NumericEntity PowerSensor { get; private set; }
    public NumericEntity StateOfChargeSensor { get; private set; }
    public NumericEntity TotalEnergyChargedSensor { get; private set; }
    public NumericEntity TotalEnergyDischargedSensor { get; private set; }

    public InputNumberEntity MaxChargePowerEntity { get; private set; }
    public bool CanCharge => MaxChargePowerEntity.State is > 0;
    public bool CanDischarge => MaxDischargePowerEntity.State is > 0;

    public InputNumberEntity MaxDischargePowerEntity { get; private set; }
    public Double CurrentLoad => PowerSensor.State ?? 0;

    //Might come in handy when using multiple batteries?
    public List<TimeWindow> TimeWindows { get; private set; }
    public String Timezone { get; private set; }

    public DateTimeOffset? LastChange { get; private set; }
    public IDisposable? StopTimer { get; set; }


    internal BatteryFileStorage ToFileStorage()
    {
        var fileStorage = new BatteryFileStorage
        {
            LastChange = LastChange,
        };
        return fileStorage;
    }

    public Battery(ILogger logger, IScheduler scheduler, string name, decimal capacity, int maxChargePower, int maxDischargePower, NumericEntity powerSensor, NumericEntity stateOfChargeSensor,
        NumericEntity totalEnergyChargedSensor, NumericEntity totalEnergyDischargedSensor, InputNumberEntity maxChargePowerEntity, InputNumberEntity maxDischargePowerEntity,
        List<TimeWindow> timeWindows, string timezone)
    {
        Logger = logger;
        Scheduler = scheduler;
        Name = name;
        Capacity = capacity;
        MaxChargePower = maxChargePower;
        MaxDischargePower = maxDischargePower;
        PowerSensor = powerSensor;
        StateOfChargeSensor = stateOfChargeSensor;
        TotalEnergyChargedSensor = totalEnergyChargedSensor;
        TotalEnergyDischargedSensor = totalEnergyDischargedSensor;
        MaxChargePowerEntity = maxChargePowerEntity;
        MaxDischargePowerEntity = maxDischargePowerEntity;
        TimeWindows = timeWindows;
        Timezone = timezone;
    }

    public void DisableCharging()
    {
        if (!CanCharge)
            return;

        MaxChargePowerEntity.Change(0);
        LastChange = Scheduler.Now;
        Logger.LogInformation("{Battery}: Battery will no longer charge.", Name);
    }

    public void EnableCharging()
    {
        if (CanCharge)
            return;

        MaxChargePowerEntity.Change(MaxChargePower);
        LastChange = Scheduler.Now;
        Logger.LogInformation("{Battery}: Battery is allowed to charge at max {maxChargePower}W.", Name, MaxChargePower);
    }

    public void DisableDischarging()
    {
        if (!CanDischarge)
            return;

        MaxDischargePowerEntity.Change(0);
        LastChange = Scheduler.Now;
        Logger.LogInformation("{Battery}: Battery will no longer discharge.", Name);
    }

    public void EnableDischarging()
    {
        if (CanDischarge)
            return;

        MaxDischargePowerEntity.Change(MaxDischargePower);
        LastChange = Scheduler.Now;
        Logger.LogInformation("{Battery}: Battery is allowed to discharge at max {maxDisChargePower}W.", Name, MaxDischargePower);
    }

    public void Dispose()
    {
        StopTimer?.Dispose();
    }
}
