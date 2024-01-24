using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SmartVentilation;

public class BathroomAirQualityGuard : IDisposable
{
    public List<NumericSensor> HumiditySensors { get; }
    public int HumidityMediumThreshold { get; }
    public int HumidityHighThreshold { get; }

    private readonly ILogger _logger;
    private readonly IScheduler _scheduler;

    public (VentilationState? State, Boolean Enforce) DesiredState { get; private set; }

    public BathroomAirQualityGuard(ILogger logger, IScheduler scheduler, List<NumericSensor> humiditySensors, Int32 humidityMediumThreshold, Int32 humidityHighThreshold)
    {
        HumiditySensors = humiditySensors;
        HumidityMediumThreshold = humidityMediumThreshold;
        HumidityHighThreshold = humidityHighThreshold;

        _logger = logger;
        _scheduler = scheduler;
    }

    public (VentilationState? State, Boolean Enforce) GetDesiredState()
    {
        if (HumiditySensors.Any(x => x.State > HumidityHighThreshold))
            return (VentilationState.High, true);

        if (HumiditySensors.Any(x => x.State > HumidityMediumThreshold))
            return (VentilationState.Medium, false);

        return (null, false);

    }

    public void Dispose()
    {
        foreach (var x in HumiditySensors)
            x.Dispose();
    }
}