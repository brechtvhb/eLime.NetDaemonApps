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

    public (VentilationState? State, Boolean Enforce) DesiredState { get; private set; }

    public IndoorAirQualityGuard(ILogger logger, IScheduler scheduler, List<NumericSensor> co2Sensors, Int32 co2MediumThreshold, Int32 co2HighThreshold)
    {
        Co2Sensors = co2Sensors;

        if (co2Sensors.Any())
        {
            foreach (var co2Sensor in Co2Sensors)
            {
                co2Sensor.Changed += CheckDesiredState;
            }
        }

        Co2MediumThreshold = co2MediumThreshold;
        Co2HighThreshold = co2HighThreshold;

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

        OnDesiredStateChanged(new DesiredStateEventArgs(VentilationGuards.IndoorAirQuality, desiredState.State, desiredState.Enforce));
    }

    public event EventHandler<DesiredStateEventArgs>? DesiredStateChanged;

    protected void OnDesiredStateChanged(DesiredStateEventArgs e)
    {
        DesiredStateChanged?.Invoke(this, e);
    }

    private (VentilationState? State, Boolean Enforce) GetDesiredState()
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