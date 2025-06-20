using eLime.NetDaemonApps.Config.EnergyManager;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using NetDaemon.HassModel;

namespace eLime.NetDaemonApps.Domain.EnergyManager.Consumers;

public class TimeWindowConfiguration
{
    public TimeWindowConfiguration(IHaContext haContext, TimeWindowConfig config)
    {
        ActiveSensor = !string.IsNullOrWhiteSpace(config.ActiveSensor) ? BinarySensor.Create(haContext, config.ActiveSensor) : null;
        Start = config.Start;
        End = config.End;
    }
    public BinarySensor? ActiveSensor { get; set; }
    public TimeSpan Start { get; set; }
    public TimeSpan End { get; set; }
}