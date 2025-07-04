﻿using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
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


    public (VentilationState? State, bool Enforce) GetDesiredState()
    {
        if (_sleepSensor.IsOn())
            return (VentilationState.Off, false);

        return _awaySensor.IsOn()
            ? (Away: VentilationState.Off, false)
            : (VentilationState.Low, false);
    }

    public void Dispose()
    {
        _awaySensor.Dispose();
        _sleepSensor.Dispose();
    }
}