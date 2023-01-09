using eLime.NetDaemonApps.Domain.Entities.NumericSensors;

namespace eLime.NetDaemonApps.Domain.FlexiScreens;

public class StormProtector
{
    private NumericThresholdSensor? WindSpeedSensor { get; }
    private double? WindSpeedStormStartThreshold { get; }
    private double? WindSpeedStormEndThreshold { get; }

    private NumericThresholdSensor? RainRateSensor { get; }
    private double? RainRateStormStartThreshold { get; }
    private double? RainRateStormEndThreshold { get; }

    private NumericThresholdSensor? ShortTermRainForecastSensor { get; }
    private double? ShortTermRainForecastSensorStormStartThreshold { get; }
    private double? ShortTermRainForecastSensorStormEndThreshold { get; }

    public (ScreenState? State, Boolean Enforce) DesiredState { get; private set; }

    public StormProtector(NumericThresholdSensor? windSpeedSensor, double? windSpeedStormStartThreshold, double? windSpeedStormEndThreshold,
        NumericThresholdSensor? rainRateSensor, double? rainRateStormStartThreshold, double? rainRateStormEndThreshold,
        NumericThresholdSensor? shortTermRainForecastSensor, double? shortTermRainForecastSensorStormStartThreshold, double? shortTermRainForecastSensorStormEndThreshold)
    {
        WindSpeedSensor = windSpeedSensor;
        if (WindSpeedSensor != null)
        {
            WindSpeedStormStartThreshold = windSpeedStormStartThreshold;
            WindSpeedStormEndThreshold = windSpeedStormEndThreshold;
            WindSpeedSensor.WentAboveThreshold += CheckStateDesiredState;
            WindSpeedSensor.DroppedBelowThreshold += CheckStateDesiredState;
        }

        RainRateSensor = rainRateSensor;
        if (RainRateSensor != null)
        {
            RainRateStormStartThreshold = rainRateStormStartThreshold;
            RainRateStormEndThreshold = rainRateStormEndThreshold;
            RainRateSensor.WentAboveThreshold += CheckStateDesiredState;
            RainRateSensor.DroppedBelowThreshold += CheckStateDesiredState;
        }

        ShortTermRainForecastSensor = shortTermRainForecastSensor;
        if (ShortTermRainForecastSensor != null)
        {
            ShortTermRainForecastSensorStormStartThreshold = shortTermRainForecastSensorStormStartThreshold;
            ShortTermRainForecastSensorStormEndThreshold = shortTermRainForecastSensorStormEndThreshold;
            ShortTermRainForecastSensor.WentAboveThreshold += CheckStateDesiredState;
            ShortTermRainForecastSensor.DroppedBelowThreshold += CheckStateDesiredState;
        }
    }

    private void CheckStateDesiredState(object? sender, NumericSensorEventArgs e)
    {
        var desiredState = GetDesiredState();

        if (DesiredState == desiredState)
            return;

        DesiredState = desiredState;
        OnDesiredStateChanged(new DesiredStateEventArgs(desiredState.State, desiredState.Enforce));
    }

    public event EventHandler<DesiredStateEventArgs>? DesiredStateChanged;

    protected void OnDesiredStateChanged(DesiredStateEventArgs e)
    {
        DesiredStateChanged?.Invoke(this, e);
    }

    public (ScreenState? State, Boolean Enforce) GetDesiredState()
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

        bool? windSpeedIsBelowStormThreshold = WindSpeedSensor == null || WindSpeedStormEndThreshold == null
            ? null
            : WindSpeedSensor.State <= WindSpeedStormStartThreshold;

        bool? rainRateIsBelowStormThreshold = RainRateSensor == null || RainRateStormEndThreshold == null
            ? null
            : RainRateSensor.State <= RainRateStormStartThreshold;

        bool? shortTermRainForecastIsBelowStormThreshold = ShortTermRainForecastSensor == null || ShortTermRainForecastSensorStormEndThreshold == null
            ? null
            : ShortTermRainForecastSensor.State <= ShortTermRainForecastSensorStormEndThreshold;

        if (windSpeedIsAboveStormThreshold == true || rainRateIsAboveStormThreshold == true || shortTermRainForecastIsAboveStormThreshold == true)
            return (ScreenState.Down, true);

        if (windSpeedIsBelowStormThreshold == true && rainRateIsBelowStormThreshold == true && shortTermRainForecastIsBelowStormThreshold == true)
            return (ScreenState.Up, false);

        return (null, false);
    }
}