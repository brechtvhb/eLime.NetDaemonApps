using eLime.NetDaemonApps.Domain.Entities.NumericSensors;

namespace eLime.NetDaemonApps.Domain.FlexiScreens;

public class StormProtector
{
    private NumericSensor? WindSpeedSensor { get; }
    private double? WindSpeedStormStartThreshold { get; }
    private double? WindSpeedStormEndThreshold { get; }

    private NumericSensor? RainRateSensor { get; }
    public double? RainRateStormStartThreshold { get; }
    public double? RainRateStormEndThreshold { get; }

    private NumericSensor? ShortTermRainForecastSensor { get; }
    public double? ShortTermRainForecastSensorStormStartThreshold { get; }
    public double? ShortTermRainForecastSensorStormEndThreshold { get; }


    public StormProtector(NumericSensor windSpeedSensor, double? windSpeedStormStartThreshold, double? windSpeedStormEndThreshold,
        NumericSensor rainRateSensor, double? rainRateStormStartThreshold, double? rainRateStormEndThreshold,
        NumericSensor shortTermRainForecastSensor, double? shortTermRainForecastSensorStormStartThreshold, double? shortTermRainForecastSensorStormEndThreshold)
    {
        WindSpeedSensor = windSpeedSensor;
        WindSpeedStormStartThreshold = windSpeedStormStartThreshold;
        WindSpeedStormEndThreshold = windSpeedStormEndThreshold;

        RainRateSensor = rainRateSensor;
        RainRateStormStartThreshold = rainRateStormStartThreshold;
        RainRateStormEndThreshold = rainRateStormEndThreshold;

        ShortTermRainForecastSensor = shortTermRainForecastSensor;
        ShortTermRainForecastSensorStormStartThreshold = shortTermRainForecastSensorStormStartThreshold;
        ShortTermRainForecastSensorStormEndThreshold = shortTermRainForecastSensorStormEndThreshold;
    }

    public (ScreenState? State, Boolean Enforce) GetDesiredState(ScreenState currentScreenState)
    {
        switch (currentScreenState)
        {
            case ScreenState.Up:
                {
                    bool? windSpeedIsAboveStormThreshold = WindSpeedSensor == null || WindSpeedStormStartThreshold == null
                        ? null
                        : WindSpeedSensor.State > WindSpeedStormStartThreshold;

                    bool? rainRateIsAboveStormThreshold = RainRateSensor == null || RainRateStormStartThreshold == null
                        ? null
                        : RainRateSensor.State > RainRateStormStartThreshold;

                    bool? shortTermRainForecastIsAboveStormThreshold = ShortTermRainForecastSensor == null || ShortTermRainForecastSensorStormStartThreshold == null
                        ? null
                        : ShortTermRainForecastSensor.State > ShortTermRainForecastSensorStormStartThreshold;

                    if (windSpeedIsAboveStormThreshold == true || rainRateIsAboveStormThreshold == true || shortTermRainForecastIsAboveStormThreshold == true)
                        return (ScreenState.Down, true);
                    break;
                }
            case ScreenState.Down:
                {
                    bool? windSpeedIsBelowStormThreshold = WindSpeedSensor == null || WindSpeedStormEndThreshold == null
                        ? null
                        : WindSpeedSensor.State <= WindSpeedStormStartThreshold;

                    bool? rainRateIsBelowStormThreshold = RainRateSensor == null || RainRateStormEndThreshold == null
                        ? null
                        : RainRateSensor.State <= RainRateStormStartThreshold;

                    bool? shortTermRainForecastIsBelowStormThreshold = ShortTermRainForecastSensor == null || ShortTermRainForecastSensorStormEndThreshold == null
                        ? null
                        : ShortTermRainForecastSensor.State <= ShortTermRainForecastSensorStormEndThreshold;

                    if (windSpeedIsBelowStormThreshold == true && rainRateIsBelowStormThreshold == true && shortTermRainForecastIsBelowStormThreshold == true)
                        return (ScreenState.Up, false);
                    break;
                }
            default:
                throw new ArgumentOutOfRangeException(nameof(currentScreenState), currentScreenState, null);
        }

        return (null, false);
    }
}