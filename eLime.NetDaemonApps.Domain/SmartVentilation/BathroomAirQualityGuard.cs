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

        if (humiditySensors.Any())
        {
            foreach (var humiditySensor in HumiditySensors)
            {
                humiditySensor.Changed += CheckDesiredState;
            }
        }

        HumidityMediumThreshold = humidityMediumThreshold;
        HumidityHighThreshold = humidityHighThreshold;

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

        OnDesiredStateChanged(new DesiredStateEventArgs(VentilationGuards.BathroomAirQuality, desiredState.State, desiredState.Enforce));
    }

    public event EventHandler<DesiredStateEventArgs>? DesiredStateChanged;

    protected void OnDesiredStateChanged(DesiredStateEventArgs e)
    {
        DesiredStateChanged?.Invoke(this, e);
    }

    private (VentilationState? State, Boolean Enforce) GetDesiredState()
    {
        if (HumiditySensors.Any(x => x.State > HumidityHighThreshold))
            return (VentilationState.High, true);

        if (HumiditySensors.Any(x => x.State > HumidityMediumThreshold))
            return (VentilationState.Medium, false);

        return (null, false);

    }

    public void Dispose()
    {
    }
}