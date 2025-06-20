using eLime.NetDaemonApps.Config;
using eLime.NetDaemonApps.Domain.Entities.Input;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using NetDaemon.HassModel;

namespace eLime.NetDaemonApps.Domain.EnergyManager.BatteryManager;

public class BatteryConfiguration
{
    public BatteryConfiguration(IHaContext haContext, BatteryConfig config)
    {
        Name = config.Name;
        Capacity = config.Capacity;
        MaxChargePower = config.MaxChargePower;
        MaxDischargePower = config.MaxDischargePower;
        PowerSensor = NumericSensor.Create(haContext, config.PowerSensor);
        StateOfChargeSensor = NumericSensor.Create(haContext, config.StateOfChargeSensor);
        TotalEnergyChargedSensor = NumericSensor.Create(haContext, config.TotalEnergyChargedSensor);
        TotalEnergyDischargedSensor = NumericSensor.Create(haContext, config.TotalEnergyDischargedSensor);
        MaxChargePowerNumber = InputNumberEntity.Create(haContext, config.MaxChargePowerEntity);
        MaxDischargePowerNumber = InputNumberEntity.Create(haContext, config.MaxDischargePowerEntity);
    }

    public string Name { get; set; }
    public decimal Capacity { get; set; } // in kWh
    public int MaxChargePower { get; set; } // in W
    public int MaxDischargePower { get; set; } // in W
    public NumericSensor PowerSensor { get; set; }
    public NumericSensor StateOfChargeSensor { get; set; }
    public NumericSensor TotalEnergyChargedSensor { get; set; } // in kWh
    public NumericSensor TotalEnergyDischargedSensor { get; set; } // in kWh
    public InputNumberEntity MaxChargePowerNumber { get; set; }
    public InputNumberEntity MaxDischargePowerNumber { get; set; }

    //To generate: OperatingMode (Automatic, Manual), ReservedPeakShavingStateOfCharge, RoundTripEfficiency
}