using eLime.NetDaemonApps.Config.EnergyManager;
using eLime.NetDaemonApps.Domain.EnergyManager;
using eLime.NetDaemonApps.Domain.Helper;

#pragma warning disable CS8618, CS9264

namespace eLime.NetDaemonApps.Tests.EnergyManager.Builders;

public class BatteryBuilder
{
    private string _name;

    private decimal _capacity;
    private int _minimumStateOfCharge;
    private List<int> _rteStateOfChargeReferencePoints;
    private int _maxChargePower;
    private int _optimalChargePowerMinThreshold;
    private int _optimalChargePowerMaxThreshold;
    private int _maxDischargePower;
    private int _optimalDischargePowerMinThreshold;
    private int _optimalDischargePowerMaxThreshold;
    private string _powerSensor;
    private string _stateOfChargeSensor;
    private string _totalEnergyChargedSensor;
    private string _totalEnergyDischargedSensor;
    private string _maxChargePowerEntity;
    private string _maxDischargePowerEntity;

    private List<TimeWindow> _timeWindows = [];


    public BatteryBuilder()
    {

    }

    public BatteryBuilder MarstekVenusE()
    {
        _name = "Marstek Venus E";
        _capacity = 5.12m; // in kWh
        _minimumStateOfCharge = 11; // in %
        _rteStateOfChargeReferencePoints = [50];
        _maxChargePower = 2500; // in W
        _optimalChargePowerMinThreshold = 200;
        _optimalChargePowerMaxThreshold = 800;
        _optimalDischargePowerMinThreshold = 200;
        _optimalDischargePowerMaxThreshold = 800;
        _maxDischargePower = 800; // in W

        return this;
    }
    public BatteryBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public BatteryBuilder WithMaxDischargePower(int power)
    {
        _maxDischargePower = power;
        return this;
    }

    public BatteryConfig Build()
    {
        _powerSensor = $"sensor.{_name.MakeHaFriendly()}_power";
        _stateOfChargeSensor = $"sensor.{_name.MakeHaFriendly()}_soc";
        _totalEnergyChargedSensor = $"sensor.{_name.MakeHaFriendly()}_total_energy_charged";
        _totalEnergyDischargedSensor = $"sensor{_name.MakeHaFriendly()}_energy_discharged";
        _maxChargePowerEntity = $"number.{_name.MakeHaFriendly()}_max_charge_power";
        _maxDischargePowerEntity = $"number.{_name.MakeHaFriendly()}_max_discharge_power";

        var battery = new BatteryConfig
        {
            Name = _name,
            Capacity = _capacity, // in kWh
            MinimumStateOfCharge = _minimumStateOfCharge, // in %
            RteStateOfChargeReferencePoints = _rteStateOfChargeReferencePoints, // in %
            MaxChargePower = _maxChargePower, // in W
            OptimalChargePowerMinThreshold = _optimalChargePowerMinThreshold, // in W
            OptimalChargePowerMaxThreshold = _optimalChargePowerMaxThreshold, // in W
            MaxDischargePower = _maxDischargePower, // in W
            OptimalDischargePowerMinThreshold = _optimalDischargePowerMinThreshold, // in W
            OptimalDischargePowerMaxThreshold = _optimalDischargePowerMaxThreshold, // in W
            PowerSensor = _powerSensor,
            StateOfChargeSensor = _stateOfChargeSensor,
            TotalEnergyChargedSensor = _totalEnergyChargedSensor,
            TotalEnergyDischargedSensor = _totalEnergyDischargedSensor,
            MaxChargePowerEntity = _maxChargePowerEntity,
            MaxDischargePowerEntity = _maxDischargePowerEntity,
        };
        return battery;
    }

}