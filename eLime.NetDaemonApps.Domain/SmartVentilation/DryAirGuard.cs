using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SmartVentilation;

public class DryAirGuard : IDisposable
{
    public List<NumericSensor> IndoorHumiditySensors { get; }
    public int LowHumidityThreshold { get; }
    public NumericSensor OutdoorTemperatureSensor { get; }
    public int MaxOutdoorTemperature { get; }

    private readonly ILogger _logger;
    private readonly IScheduler _scheduler;

    public DryAirGuard(ILogger logger, IScheduler scheduler, List<NumericSensor> indoorHumiditySensors, int lowHumidityThreshold, NumericSensor outdoorTemperatureSensor, int maxOutdoorTemperature)
    {
        IndoorHumiditySensors = indoorHumiditySensors;

        LowHumidityThreshold = lowHumidityThreshold;
        OutdoorTemperatureSensor = outdoorTemperatureSensor;
        MaxOutdoorTemperature = maxOutdoorTemperature;

        _logger = logger;
        _scheduler = scheduler;
    }

    public (VentilationState? State, bool Enforce) GetDesiredState()
    {
        if (OutdoorTemperatureSensor.State > MaxOutdoorTemperature)
            return (null, false);

        if (IndoorHumiditySensors.Any(x => x.State < LowHumidityThreshold))
            return (VentilationState.Off, true);

        return (null, false);

    }

    public void Dispose()
    {
        foreach (var x in IndoorHumiditySensors)
            x.Dispose();

        OutdoorTemperatureSensor?.Dispose();
    }
}