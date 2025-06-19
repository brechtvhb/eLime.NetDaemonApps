using eLime.NetDaemonApps.Config.EnergyManager;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using NetDaemon.HassModel;

namespace eLime.NetDaemonApps.Domain.EnergyManager2.Consumers.Cooling;

public class CoolingEnergyConsumerConfiguration
{
    public CoolingEnergyConsumerConfiguration(IHaContext haContext, CoolingEnergyConsumerConfig config)
    {
        SocketSwitch = BinarySwitch.Create(haContext, config.SocketEntity);
        TemperatureSensor = NumericSensor.Create(haContext, config.TemperatureSensor);
        TargetTemperature = config.TargetTemperature;
        MaxTemperature = config.MaxTemperature;
        PeakLoad = config.PeakLoad;
    }
    public BinarySwitch SocketSwitch { get; set; }
    public NumericSensor TemperatureSensor { get; set; }
    public double TargetTemperature { get; set; }
    public double MaxTemperature { get; set; }
    public double PeakLoad { get; set; }
}