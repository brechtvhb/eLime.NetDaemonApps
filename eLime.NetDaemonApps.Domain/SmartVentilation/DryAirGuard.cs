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

    public (VentilationState? State, Boolean Enforce) DesiredState { get; private set; }

    public DryAirGuard(ILogger logger, IScheduler scheduler, List<NumericSensor> indoorHumiditySensors, Int32 lowHumidityThreshold, NumericSensor outdoorTemperatureSensor, Int32 maxOutdoorTemperature)
    {
        IndoorHumiditySensors = indoorHumiditySensors;

        if (indoorHumiditySensors.Any())
        {
            foreach (var sensor in IndoorHumiditySensors)
            {
                sensor.Changed += CheckDesiredState;
            }
        }

        LowHumidityThreshold = lowHumidityThreshold;
        OutdoorTemperatureSensor = outdoorTemperatureSensor;
        MaxOutdoorTemperature = maxOutdoorTemperature;

        _logger = logger;
        _scheduler = scheduler;
    }

    private void CheckDesiredState(object? sender, NumericSensorEventArgs e)
    {
        CheckDesiredState();
    }

    internal void CheckDesiredState(Boolean emitEvent = true)
    {
        var desiredState = GetDesiredState();

        if (DesiredState == desiredState)
            return;

        DesiredState = desiredState;

        if (!emitEvent)
            return;

        OnDesiredStateChanged(new DesiredStateEventArgs(VentilationGuards.DryAir, desiredState.State, desiredState.Enforce));
    }

    public event EventHandler<DesiredStateEventArgs>? DesiredStateChanged;

    protected void OnDesiredStateChanged(DesiredStateEventArgs e)
    {
        DesiredStateChanged?.Invoke(this, e);
    }

    private (VentilationState? State, Boolean Enforce) GetDesiredState()
    {
        if (OutdoorTemperatureSensor.State > MaxOutdoorTemperature)
            return (null, false);

        if (IndoorHumiditySensors.Any(x => x.State < LowHumidityThreshold))
            return (VentilationState.Off, false);

        return (null, false);

    }

    public void Dispose()
    {
    }
}