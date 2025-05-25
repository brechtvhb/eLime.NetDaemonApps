using eLime.NetDaemonApps.Domain.EnergyManager;
using eLime.NetDaemonApps.Domain.Entities.Input;
using eLime.NetDaemonApps.Tests.Helpers;
using Microsoft.Extensions.Logging;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Tests.Builders;

public class BatteryBuilder
{
    private readonly ILogger _logger;
    private readonly AppTestContext _testCtx;
    private String _name;

    private decimal _capacity;
    private int _maxChargePower;
    private int _maxDischargePower;
    private NumericEntity _powerSensor;
    private NumericEntity _stateOfChargeSensor;
    private NumericEntity _totalEnergyChargedSensor;
    private NumericEntity _totalEnergyDischargedSensor;
    private InputNumberEntity _maxChargePowerEntity;
    private InputNumberEntity _maxDischargePowerEntity;

    private List<TimeWindow> _timeWindows = [];
    private String _timezone;

    public BatteryBuilder(ILogger logger, AppTestContext testCtx)
    {
        _logger = logger;
        _testCtx = testCtx;
        _timezone = "Utc";
    }
    public BatteryBuilder MarstekVenusE()
    {
        _name = "Marstek Venus E";
        _capacity = 5.12m; // in kWh
        _maxChargePower = 2500; // in W
        _maxDischargePower = 800; // in W
        _powerSensor = new NumericEntity(_testCtx.HaContext, "sensor.marstek_venus_e_power");
        _stateOfChargeSensor = new NumericEntity(_testCtx.HaContext, "sensor.marstek_venus_e_soc");
        _totalEnergyChargedSensor = new NumericEntity(_testCtx.HaContext, "sensor.marstek_venus_e_total_energy_charged");
        _totalEnergyDischargedSensor = new NumericEntity(_testCtx.HaContext, "sensor.marstek_venus_e_total_energy_discharged");
        _maxChargePowerEntity = InputNumberEntity.Create(_testCtx.HaContext, "number.marstek_venus_e_max_charge_power");
        _maxDischargePowerEntity = InputNumberEntity.Create(_testCtx.HaContext, "number.marstek_venus_e_max_discharge_power");

        return this;
    }

    public Battery Build()
    {
        var battery = new Battery(_logger, _testCtx.Scheduler, _name, _capacity, _maxChargePower, _maxDischargePower,
            _powerSensor, _stateOfChargeSensor, _totalEnergyChargedSensor, _totalEnergyDischargedSensor,
            _maxChargePowerEntity, _maxDischargePowerEntity, _timeWindows, _timezone);
        return battery;
    }

}