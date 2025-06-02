using eLime.NetDaemonApps.Config.EnergyManager;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.Entities.TextSensors;
using NetDaemon.HassModel;

namespace eLime.NetDaemonApps.Domain.EnergyManager2.Configuration;

public class CarChargerEnergyConsumerConfiguration
{
    public CarChargerEnergyConsumerConfiguration(IHaContext haContext, CarChargerEnergyConsumerConfig config)
    {
        MinimumCurrent = config.MinimumCurrent;
        MaximumCurrent = config.MaximumCurrent;
        OffCurrent = config.OffCurrent;
        CurrentSensor = NumericSensor.Create(haContext, config.CurrentEntity);
        VoltageSensor = NumericSensor.Create(haContext, config.VoltageEntity);
        StateSensor = TextSensor.Create(haContext, config.StateSensor);
        Cars = config.Cars.Select(x => new CarConfiguration(haContext, x)).ToList();
    }
    public int MinimumCurrent { get; set; }
    public int MaximumCurrent { get; set; }
    public int OffCurrent { get; set; }
    public NumericSensor CurrentSensor { get; set; }
    public NumericSensor VoltageSensor { get; set; }
    public TextSensor StateSensor { get; set; }
    public List<CarConfiguration> Cars { get; set; }
}