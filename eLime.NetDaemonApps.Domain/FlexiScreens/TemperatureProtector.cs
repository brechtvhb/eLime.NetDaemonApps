using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.Entities.Weather;

namespace eLime.NetDaemonApps.Domain.FlexiScreens;

public class TemperatureProtector : IDisposable
{
    private NumericThresholdSensor? SolarLuxSensor { get; }
    public double? SolarLuxAboveThreshold { get; set; }
    public double? SolarLuxBelowThreshold { get; set; }

    private NumericSensor? IndoorTemperatureSensor { get; }
    private Weather? Weather { get; }

    public double? MaxIndoorTemperature { get; }
    public double? ConditionalMaxIndoorTemperature { get; }
    public double? ConditionalMaxOutdoorTemperaturePrediction { get; }
    public int? ConditionalOutdoorTemperaturePredictionDays { get; }

    public (ScreenState? State, Boolean Enforce) DesiredState { get; private set; }

    public TemperatureProtector(NumericThresholdSensor? solarLuxSensor, double? solarLuxAboveThreshold, double? solarLuxBelowThreshold,
        NumericSensor? indoorTemperatureSensor, double? maxIndoorTemperature, double? maxConditionalIndoorTemperature,
        Weather? weather, double? conditionalMaxOutdoorTemperaturePrediction, int? conditionalOutdoorTemperaturePredictionDays)
    {
        SolarLuxSensor = solarLuxSensor;
        if (SolarLuxSensor != null)
        {
            SolarLuxAboveThreshold = solarLuxAboveThreshold;
            SolarLuxBelowThreshold = solarLuxBelowThreshold;
            SolarLuxSensor.WentAboveThreshold += CheckDesiredState;
            SolarLuxSensor.DroppedBelowThreshold += CheckDesiredState;
        }

        IndoorTemperatureSensor = indoorTemperatureSensor;
        if (IndoorTemperatureSensor != null)
        {
            MaxIndoorTemperature = maxIndoorTemperature;
            ConditionalMaxIndoorTemperature = maxConditionalIndoorTemperature;
            IndoorTemperatureSensor.Changed += CheckDesiredState;
        }

        Weather = weather;
        if (Weather != null)
        {
            ConditionalMaxOutdoorTemperaturePrediction = conditionalMaxOutdoorTemperaturePrediction;
            ConditionalOutdoorTemperaturePredictionDays = conditionalOutdoorTemperaturePredictionDays ?? 3;
        }

        CheckDesiredState();
    }

    private void CheckDesiredState(Object? o, NumericSensorEventArgs sender)
    {
        CheckDesiredState();
    }


    private void CheckDesiredState()
    {
        var desiredState = GetDesiredState();

        if (DesiredState == desiredState)
            return;

        DesiredState = desiredState;
        OnDesiredStateChanged(new DesiredStateEventArgs(Protectors.TemperatureProtector, desiredState.State, desiredState.Enforce));
    }

    public event EventHandler<DesiredStateEventArgs>? DesiredStateChanged;

    protected void OnDesiredStateChanged(DesiredStateEventArgs e)
    {
        DesiredStateChanged?.Invoke(this, e);
    }

    public (ScreenState? State, Boolean Enforce) GetDesiredState()
    {
        double? averagePredictedTemperature = null;

        if (Weather?.Attributes?.Forecast != null && ConditionalOutdoorTemperaturePredictionDays != null)
            averagePredictedTemperature = Weather.Attributes.Forecast.Take(ConditionalOutdoorTemperaturePredictionDays.Value).Average(x => x.Temperature);

        var solarLuxIndicatingSunIsShining = SolarLuxSensor != null && SolarLuxAboveThreshold != null && SolarLuxSensor.State > SolarLuxAboveThreshold;
        var solarLuxIndicatingSunIsNotShining = SolarLuxSensor != null && SolarLuxBelowThreshold != null && SolarLuxSensor.State <= SolarLuxBelowThreshold;

        var tooHotInside = IndoorTemperatureSensor != null && MaxIndoorTemperature != null && IndoorTemperatureSensor.State > MaxIndoorTemperature;
        var hotDaysAhead = IndoorTemperatureSensor != null && ConditionalMaxIndoorTemperature != null && averagePredictedTemperature != null && ConditionalMaxOutdoorTemperaturePrediction != null
                           && IndoorTemperatureSensor.State > ConditionalMaxIndoorTemperature && averagePredictedTemperature > ConditionalMaxOutdoorTemperaturePrediction;

        if (solarLuxIndicatingSunIsShining && (tooHotInside || hotDaysAhead))
            return (ScreenState.Down, false);

        if (solarLuxIndicatingSunIsNotShining)
            return (ScreenState.Up, false);

        return (ScreenState.Up, false);
    }

    public void Dispose()
    {
        if (SolarLuxSensor != null)
        {
            SolarLuxSensor.WentAboveThreshold -= CheckDesiredState;
            SolarLuxSensor.DroppedBelowThreshold -= CheckDesiredState;
            SolarLuxSensor.Dispose();
        }

        if (IndoorTemperatureSensor != null)
        {
            IndoorTemperatureSensor.Changed -= CheckDesiredState;
            IndoorTemperatureSensor.Dispose();
        }
    }
}