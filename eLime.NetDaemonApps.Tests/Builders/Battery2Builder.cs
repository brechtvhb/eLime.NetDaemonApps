using eLime.NetDaemonApps.Config;
using eLime.NetDaemonApps.Domain.EnergyManager;
using eLime.NetDaemonApps.Tests.Helpers;
using Microsoft.Extensions.Logging;

namespace eLime.NetDaemonApps.Tests.Builders;

public class Battery2Builder
{
    private string _name;

    private decimal _capacity;
    private int _maxChargePower;
    private int _maxDischargePower;
    private string _powerSensor;
    private string _stateOfChargeSensor;
    private string _totalEnergyChargedSensor;
    private string _totalEnergyDischargedSensor;
    private string _maxChargePowerEntity;
    private string _maxDischargePowerEntity;

    private List<TimeWindow> _timeWindows = [];


    public Battery2Builder(ILogger logger, AppTestContext testCtx)
    {

    }

    public Battery2Builder MarstekVenusE()
    {
        _name = "Marstek Venus E";
        _capacity = 5.12m; // in kWh
        _maxChargePower = 2500; // in W
        _maxDischargePower = 800; // in W
        _powerSensor = "sensor.marstek_venus_e_power";
        _stateOfChargeSensor = "sensor.marstek_venus_e_soc";
        _totalEnergyChargedSensor = "sensor.marstek_venus_e_total_energy_charged";
        _totalEnergyDischargedSensor = "sensor.marstek_venus_e_total_energy_discharged";
        _maxChargePowerEntity = "number.marstek_venus_e_max_charge_power";
        _maxDischargePowerEntity = "number.marstek_venus_e_max_discharge_power";

        return this;
    }

    public BatteryConfig Create()
    {
        var battery = new BatteryConfig
        {
            Name = _name,
            Capacity = _capacity, // in kWh
            MaxChargePower = _maxChargePower, // in W
            MaxDischargePower = _maxDischargePower, // in W
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