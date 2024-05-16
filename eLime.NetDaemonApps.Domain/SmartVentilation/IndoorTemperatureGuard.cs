using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.ClimateEntities;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SmartVentilation;

public class IndoorTemperatureGuard : IDisposable
{
    public BinarySensor SummerModeSensor { get; }
    public NumericSensor OutdoorTemperatureSensor { get; }
    private readonly ILogger _logger;
    private readonly IScheduler _scheduler;


    public IndoorTemperatureGuard(ILogger logger, IScheduler scheduler, BinarySensor summerModeSensor, NumericSensor outdoorTemperatureSensor)
    {
        SummerModeSensor = summerModeSensor;
        OutdoorTemperatureSensor = outdoorTemperatureSensor;

        _logger = logger;
        _scheduler = scheduler;
    }

    public (VentilationState? State, Boolean Enforce) GetDesiredState(Climate climate)
    {
        if (SummerModeSensor.IsOn() && climate.Attributes != null)
        {
            var isCouldEnoughOutside = OutdoorTemperatureSensor.State + 3 < climate.Attributes.Temperature;
            var isHotInside = climate.Attributes.CurrentTemperature - 1 >= climate.Attributes.Temperature;
            var isVeryHotInside = climate.Attributes.CurrentTemperature - 2 >= climate.Attributes.Temperature;

            if (isVeryHotInside && isCouldEnoughOutside)
                return (VentilationState.Medium, true);

            if (isHotInside && isCouldEnoughOutside)
                return (VentilationState.Low, true);
        }

        return (null, false);

    }

    public void Dispose()
    {
        SummerModeSensor.Dispose();
        OutdoorTemperatureSensor.Dispose();
    }
}