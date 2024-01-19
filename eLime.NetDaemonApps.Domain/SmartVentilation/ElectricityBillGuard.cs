using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SmartVentilation;

public class ElectricityBillGuard : IDisposable
{
    private readonly ILogger _logger;
    private readonly IScheduler _scheduler;
    private readonly BinarySensor _awaySensor;
    private readonly BinarySensor _sleepSensor;


    public ElectricityBillGuard(ILogger logger, IScheduler scheduler, BinarySensor awaySensor, BinarySensor sleepSensor)
    {
        _logger = logger;
        _scheduler = scheduler;
        _awaySensor = awaySensor;
        _sleepSensor = sleepSensor;
    }


    private (VentilationState? State, Boolean Enforce) GetDesiredState()
    {
        if (_sleepSensor.IsOn())
            return (VentilationState.Off, false);

        if (_awaySensor.IsOn())
            return (VentilationState.Off, false);

        return (null, false);

    }

    public void Dispose()
    {
    }
}