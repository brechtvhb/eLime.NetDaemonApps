using eLime.NetDaemonApps.Config.EnergyManager;
using eLime.NetDaemonApps.Domain.Entities.Input;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using NetDaemon.HassModel;

namespace eLime.NetDaemonApps.Domain.EnergyManager.BatteryManager.Batteries;

public class BatteryConfiguration(IHaContext haContext, BatteryConfig config)
{
    public string Name { get; set; } = config.Name;
    public decimal Capacity { get; set; } = config.Capacity; // in kWh
    public int MinimumStateOfCharge { get; set; } = config.MinimumStateOfCharge; // in %
    public List<int> RteStateOfChargeReferencePoints { get; set; } = config.RteStateOfChargeReferencePoints; // in %
    public int MaxChargePower { get; set; } = config.MaxChargePower; // in W
    public int OptimalChargePowerMinThreshold { get; set; } = config.OptimalChargePowerMinThreshold; // in W
    public int OptimalChargePowerMaxThreshold { get; set; } = config.OptimalChargePowerMaxThreshold; // in W
    public int MaxDischargePower { get; set; } = config.MaxDischargePower; // in W
    public int OptimalDischargePowerMinThreshold { get; set; } = config.OptimalDischargePowerMinThreshold; // in W
    public int OptimalDischargePowerMaxThreshold { get; set; } = config.OptimalDischargePowerMaxThreshold; // in W
    public NumericSensor PowerSensor { get; set; } = NumericSensor.Create(haContext, config.PowerSensor);
    public NumericSensor StateOfChargeSensor { get; set; } = NumericSensor.Create(haContext, config.StateOfChargeSensor);
    public NumericSensor TotalEnergyChargedSensor { get; set; } = NumericSensor.Create(haContext, config.TotalEnergyChargedSensor); // in kWh
    public NumericSensor TotalEnergyDischargedSensor { get; set; } = NumericSensor.Create(haContext, config.TotalEnergyDischargedSensor); // in kWh
    public InputNumberEntity MaxChargePowerNumber { get; set; } = InputNumberEntity.Create(haContext, config.MaxChargePowerEntity);
    public InputNumberEntity MaxDischargePowerNumber { get; set; } = InputNumberEntity.Create(haContext, config.MaxDischargePowerEntity);

    //To generate: OperatingMode (Automatic, Manual), ReservedPeakShavingStateOfCharge, RoundTripEfficiency
}