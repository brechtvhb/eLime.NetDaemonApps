using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SmartVentilation;

public class IndoorAirQualityGuard : IDisposable
{
    public List<NumericSensor> Co2Sensors { get; }
    public int Co2MediumThreshold { get; }
    public int Co2HighThreshold { get; }
    private readonly ILogger _logger;
    private readonly IScheduler _scheduler;


    public IndoorAirQualityGuard(ILogger logger, IScheduler scheduler, List<NumericSensor> co2Sensors, Int32 co2MediumThreshold, Int32 co2HighThreshold)
    {
        Co2Sensors = co2Sensors;
        Co2MediumThreshold = co2MediumThreshold;
        Co2HighThreshold = co2HighThreshold;

        _logger = logger;
        _scheduler = scheduler;
    }

    public (VentilationState? State, Boolean Enforce) GetDesiredState()
    {
        if (Co2Sensors.Any(x => x.State > Co2HighThreshold))
            return (VentilationState.High, true);

        if (Co2Sensors.Any(x => x.State > Co2MediumThreshold))
            return (VentilationState.Medium, false);

        return (null, false);

    }

    public void Dispose()
    {
    }
}