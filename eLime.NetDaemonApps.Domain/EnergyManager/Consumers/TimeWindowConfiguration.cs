using eLime.NetDaemonApps.Config.EnergyManager;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using NetDaemon.HassModel;

namespace eLime.NetDaemonApps.Domain.EnergyManager.Consumers;

public class TimeWindowConfiguration
{
    public TimeWindowConfiguration(IHaContext haContext, TimeWindowConfig config)
    {
        ActiveSensor = !string.IsNullOrWhiteSpace(config.ActiveSensor) ? BinarySensor.Create(haContext, config.ActiveSensor) : null;
        Days = config.Days;
        Start = new TimeOnly(0, 0).Add(config.Start);
        End = new TimeOnly(0, 0).Add(config.End);
    }
    public BinarySensor? ActiveSensor { get; }
    public List<DayOfWeek> Days { get; }
    public TimeOnly Start { get; }
    public TimeOnly End { get; }
}