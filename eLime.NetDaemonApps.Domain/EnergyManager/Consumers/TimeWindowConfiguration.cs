using eLime.NetDaemonApps.Config.EnergyManager;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using NetDaemon.HassModel;

namespace eLime.NetDaemonApps.Domain.EnergyManager.Consumers;

public class TimeWindowConfiguration
{
    public TimeWindowConfiguration(IHaContext haContext, TimeWindowConfig config)
    {
        (ActiveSensor, ActiveSensorInverted) = config.ActiveSensor switch
        {
            null => (null, false),
            not null when config.ActiveSensor.StartsWith("!") => (BinarySensor.Create(haContext, config.ActiveSensor[1..]), true),
            _ => (BinarySensor.Create(haContext, config.ActiveSensor), false),
        };
        Days = config.Days;
        Start = new TimeOnly(0, 0).Add(config.Start);
        End = new TimeOnly(0, 0).Add(config.End);
    }
    public BinarySensor? ActiveSensor { get; }
    public bool ActiveSensorInverted { get; }
    public List<DayOfWeek> Days { get; }
    public TimeOnly Start { get; }
    public TimeOnly End { get; }
}