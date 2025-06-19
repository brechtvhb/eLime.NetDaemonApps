using eLime.NetDaemonApps.Config.EnergyManager;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using NetDaemon.HassModel;

namespace eLime.NetDaemonApps.Domain.EnergyManager2.Consumers.Simple;

public class SimpleEnergyConsumerConfiguration
{
    public SimpleEnergyConsumerConfiguration(IHaContext haContext, SimpleEnergyConsumerConfig config)
    {
        SocketSwitch = BinarySwitch.Create(haContext, config.SocketEntity);
        PeakLoad = config.PeakLoad;
    }
    public BinarySwitch SocketSwitch { get; set; }
    public double PeakLoad { get; set; }
}