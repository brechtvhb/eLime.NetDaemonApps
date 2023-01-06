using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.Entities.Weather;

namespace eLime.NetDaemonApps.Domain.FlexiScreens;

public class TemperatureProtector
{
    private NumericSensor SolarLuxSensor { get; }
    public int? SolarLuxAboveThreshold { get; set; }
    public int? SolarLuxBelowThreshold { get; set; }

    private NumericSensor IndoorTemperatureSensor { get; }
    private Weather Weather { get; }

    public double? MaxIndoorTemperature { get; }
    public double? ConditionalMaxIndoorTemperature { get; }
    public double? ConditionalMaxOutdoorTemperaturePrediction { get; }
    public int? ConditionalOutdoorTemperaturePredictionDays { get; }

    public TemperatureProtector(NumericSensor solarLuxSensor, int? solarLuxAboveThreshold, int? solarLuxBelowThreshold,
        NumericSensor indoorTemperature, double? maxIndoorTemperature, double? maxConditionalIndoorTemperature,
        Weather weather, double? conditionalMaxOutdoorTemperaturePrediction, int? conditionalOutdoorTemperaturePredictionDays)
    {
        SolarLuxSensor = solarLuxSensor;
        SolarLuxAboveThreshold = solarLuxAboveThreshold;
        SolarLuxBelowThreshold = solarLuxBelowThreshold;

        IndoorTemperatureSensor = indoorTemperature;
        MaxIndoorTemperature = maxIndoorTemperature;
        ConditionalMaxIndoorTemperature = maxConditionalIndoorTemperature;

        Weather = weather;
        ConditionalMaxOutdoorTemperaturePrediction = conditionalMaxOutdoorTemperaturePrediction;
        ConditionalOutdoorTemperaturePredictionDays = conditionalOutdoorTemperaturePredictionDays ?? 3;
    }

    public (ScreenState? State, Boolean Enforce) GetDesiredState(ScreenState currentScreenState)
    {
        //TODO
        int? averagePredictedTemperature = null; //Weather.Attributes.Forecast;

        switch (currentScreenState)
        {
            case ScreenState.Up:
                {
                    var solarLuxIndicatingSunIsShining = SolarLuxSensor != null && SolarLuxAboveThreshold != null && SolarLuxSensor.State > SolarLuxAboveThreshold;
                    var tooHotInside = IndoorTemperatureSensor != null && MaxIndoorTemperature != null && IndoorTemperatureSensor.State > MaxIndoorTemperature;
                    var hotDaysAhead = IndoorTemperatureSensor != null && ConditionalMaxIndoorTemperature != null && averagePredictedTemperature != null && ConditionalMaxOutdoorTemperaturePrediction != null && IndoorTemperatureSensor.State > ConditionalMaxIndoorTemperature && averagePredictedTemperature > ConditionalMaxOutdoorTemperaturePrediction;

                    return solarLuxIndicatingSunIsShining switch
                    {
                        true when tooHotInside || hotDaysAhead => (ScreenState.Down, false),
                        _ => (ScreenState.Up, false)
                    };
                }
            case ScreenState.Down:
                {
                    var solarLuxIndicatingSunIsNotShining = SolarLuxSensor != null && SolarLuxBelowThreshold != null && SolarLuxSensor.State <= SolarLuxBelowThreshold;

                    return solarLuxIndicatingSunIsNotShining
                        ? (ScreenState.Up, false)
                        : (ScreenState.Down, false);
                }
            default:
                throw new ArgumentOutOfRangeException(nameof(currentScreenState), currentScreenState, null);
        }
    }
}