using eLime.NetDaemonApps.Config.EnergyManager;

namespace eLime.NetDaemonApps.Config;

public class EnergyManagerConfig
{
    public string Timezone { get; set; }
    public GridConfig Grid { get; set; }
    public string SolarProductionRemainingTodayEntity { get; set; }
    public string PhoneToNotify { get; set; }
    public List<EnergyConsumerConfig> Consumers { get; set; }
    public BatteryManagerConfig BatteryManager { get; set; }

}

public class BatteryManagerConfig
{
    public string TotalChargePowerSensor { get; set; }
    public string TotalDischargePowerSensor { get; set; }

    //to generate: TotalCapacity, RemainingCapacity, AggregatedStateOfCharge

    public List<BatteryConfig> Batteries { get; set; }

}

public class BatteryConfig
{
    public string Name { get; set; }
    public decimal Capacity { get; set; } //in kWh
    //public int InitialStateOfCharge { get; set; } nope, recalculate RTE everytime battery percentage hits 11%?
    public int MaxChargePower { get; set; }
    public int MaxDischargePower { get; set; }
    public string PowerSensor { get; set; }
    public string StateOfChargeSensor { get; set; }
    public string TotalEnergyChargedSensor { get; set; }
    public string TotalEnergyDischargedSensor { get; set; }

    public string MaxChargePowerEntity { get; set; }
    public string MaxDischargePowerEntity { get; set; }

    //to generate: OperatingMode (Automatic, Manual), ReservedPeakShavingStateOfCharge, RoundTripEfficiency

}