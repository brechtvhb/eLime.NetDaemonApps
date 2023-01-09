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

        var solarLuxIndicatingSunIsShining = SolarLuxSensor != null && SolarLuxAboveThreshold != null && SolarLuxSensor.State > SolarLuxAboveThreshold;
        var solarLuxIndicatingSunIsNotShining = SolarLuxSensor != null && SolarLuxBelowThreshold != null && SolarLuxSensor.State <= SolarLuxBelowThreshold;

        var tooHotInside = IndoorTemperatureSensor != null && MaxIndoorTemperature != null && IndoorTemperatureSensor.State > MaxIndoorTemperature;
        var hotDaysAhead = IndoorTemperatureSensor != null && ConditionalMaxIndoorTemperature != null && averagePredictedTemperature != null && ConditionalMaxOutdoorTemperaturePrediction != null && IndoorTemperatureSensor.State > ConditionalMaxIndoorTemperature && averagePredictedTemperature > ConditionalMaxOutdoorTemperaturePrediction;

        if (solarLuxIndicatingSunIsShining && (tooHotInside || hotDaysAhead))
            return (ScreenState.Down, false);

        if (solarLuxIndicatingSunIsNotShining)
            return (ScreenState.Up, false);

        return (null, false);
    }
}