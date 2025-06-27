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
    public NumericSensor PostHeatExchangerTemperatureSensor { get; }
    private readonly ILogger _logger;
    private readonly IScheduler _scheduler;


    public IndoorTemperatureGuard(ILogger logger, IScheduler scheduler, BinarySensor summerModeSensor, NumericSensor outdoorTemperatureSensor, NumericSensor postHeatExchangerTemperatureSensor)
    {
        SummerModeSensor = summerModeSensor;
        OutdoorTemperatureSensor = outdoorTemperatureSensor;
        PostHeatExchangerTemperatureSensor = postHeatExchangerTemperatureSensor;

        _logger = logger;
        _scheduler = scheduler;
    }

    public (VentilationState? State, bool Enforce) GetDesiredState(Climate climate)
    {
        if (!SummerModeSensor.IsOn() || climate.Attributes == null)
            return (null, false);

        var isColdEnoughOutside = OutdoorTemperatureSensor.State + 3 <= climate.Attributes.CurrentTemperature;
        var isColdPastHeatExchanger = PostHeatExchangerTemperatureSensor.State + 2 <= climate.Attributes.CurrentTemperature;
        var isHotInside = climate.Attributes.CurrentTemperature - 1 >= climate.Attributes.Temperature;
        var isVeryHotInside = climate.Attributes.CurrentTemperature - 2 >= climate.Attributes.Temperature;

        _logger.LogInformation("Indoor temperature guard: isCouldEnoughOutside: {isColdEnoughOutside}. isColdPastHeatExchanger: {isColdPastHeatExchanger}.  isHotInside: {isHotInside}. isVeryHotInside: {isVeryHotInside}", isColdEnoughOutside, isColdPastHeatExchanger, isHotInside, isVeryHotInside);

        if (isVeryHotInside && (isColdEnoughOutside || isColdPastHeatExchanger))
            return (VentilationState.Medium, true);

        if (isHotInside && (isColdEnoughOutside || isColdPastHeatExchanger))
            return (VentilationState.Low, true);

        return (null, false);

    }

    public void Dispose()
    {
        SummerModeSensor.Dispose();
        OutdoorTemperatureSensor.Dispose();
        PostHeatExchangerTemperatureSensor?.Dispose();
    }
}