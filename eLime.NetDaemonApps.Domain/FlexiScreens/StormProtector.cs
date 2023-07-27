using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.Entities.Weather;

namespace eLime.NetDaemonApps.Domain.FlexiScreens;

public class StormProtector : IDisposable
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

    private Weather? HourlyWeather { get; }
    private int? NightlyPredictionHours { get; }
    private double? NightlyWindSpeedThreshold { get; }
    private double? NightlyRainThreshold { get; }
    private double? NightlyRainRateThreshold { get; }

    public (ScreenState? State, Boolean Enforce) DesiredState { get; private set; }
    private bool StormModeActive { get; set; }
    private bool Night { get; set; }
    public bool StormyNight { get; private set; }

    public StormProtector(NumericThresholdSensor? windSpeedSensor, double? windSpeedStormStartThreshold, double? windSpeedStormEndThreshold,
        NumericThresholdSensor? rainRateSensor, double? rainRateStormStartThreshold, double? rainRateStormEndThreshold,
        NumericThresholdSensor? shortTermRainForecastSensor, double? shortTermRainForecastSensorStormStartThreshold, double? shortTermRainForecastSensorStormEndThreshold,
        Weather? hourlyWeather, int? nightlyPredictionHours, double? nightlyWindSpeedThreshold, double? nightlyRainThreshold, double? nightlyRainRateThreshold)
    {
        WindSpeedSensor = windSpeedSensor;
        if (WindSpeedSensor != null)
        {
            WindSpeedStormStartThreshold = windSpeedStormStartThreshold;
            WindSpeedStormEndThreshold = windSpeedStormEndThreshold;
            WindSpeedSensor.WentAboveThreshold += CheckDesiredState;
            WindSpeedSensor.DroppedBelowThreshold += CheckDesiredState;
        }

        RainRateSensor = rainRateSensor;
        if (RainRateSensor != null)
        {
            RainRateStormStartThreshold = rainRateStormStartThreshold;
            RainRateStormEndThreshold = rainRateStormEndThreshold;
            RainRateSensor.WentAboveThreshold += CheckDesiredState;
            RainRateSensor.DroppedBelowThreshold += CheckDesiredState;
        }

        ShortTermRainForecastSensor = shortTermRainForecastSensor;
        if (ShortTermRainForecastSensor != null)
        {
            ShortTermRainForecastSensorStormStartThreshold = shortTermRainForecastSensorStormStartThreshold;
            ShortTermRainForecastSensorStormEndThreshold = shortTermRainForecastSensorStormEndThreshold;
            ShortTermRainForecastSensor.WentAboveThreshold += CheckDesiredState;
            ShortTermRainForecastSensor.DroppedBelowThreshold += CheckDesiredState;
        }

        HourlyWeather = hourlyWeather;
        if (HourlyWeather != null)
        {
            NightlyPredictionHours = nightlyPredictionHours ?? 12;
            NightlyWindSpeedThreshold = nightlyWindSpeedThreshold;
            NightlyRainThreshold = nightlyRainThreshold;
            NightlyRainRateThreshold = nightlyRainRateThreshold;
        }
    }

    private void CheckDesiredState(Object? o, NumericSensorEventArgs sender)
    {
        CheckDesiredState();
    }

    internal void CheckDesiredState()
    {
        var desiredState = GetDesiredState();

        if (DesiredState == desiredState)
            return;

        DesiredState = desiredState;
        OnDesiredStateChanged(new DesiredStateEventArgs(Protectors.StormProtector, desiredState.State, desiredState.Enforce));
    }

    public event EventHandler<DesiredStateEventArgs>? DesiredStateChanged;

    protected void OnDesiredStateChanged(DesiredStateEventArgs e)
    {
        DesiredStateChanged?.Invoke(this, e);
    }

    //To set stormy night on reboot
    public void SetStormyNight()
    {
        Night = true;
        StormyNight = true;
        CheckDesiredState();
    }

    public void CheckForStormyNight()
    {
        Night = true;

        if (HourlyWeather == null) return;

        double? maxWindSpeed = null;
        double? totalPrecipitation = null;
        double? maxPrecipitationRate = null;

        if (HourlyWeather?.Attributes?.Forecast != null && NightlyPredictionHours != null)
            maxWindSpeed = HourlyWeather.Attributes.Forecast.Take(NightlyPredictionHours.Value).Max(x => x.WindSpeed);

        if (HourlyWeather?.Attributes?.Forecast != null && NightlyPredictionHours != null)
            totalPrecipitation = HourlyWeather.Attributes.Forecast.Take(NightlyPredictionHours.Value).Sum(x => x.Precipitation);

        if (HourlyWeather?.Attributes?.Forecast != null && NightlyPredictionHours != null)
            maxPrecipitationRate = HourlyWeather.Attributes.Forecast.Take(NightlyPredictionHours.Value).Max(x => x.Precipitation);

        StormyNight = maxWindSpeed >= NightlyWindSpeedThreshold || totalPrecipitation >= NightlyRainThreshold || maxPrecipitationRate >= NightlyRainRateThreshold || StormyNight; //Could already be set from SetStormyNight on init of flexiscreen

        CheckDesiredState();
    }

    public void EndNight()
    {
        Night = false;
        StormyNight = false;

        CheckDesiredState();
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
        {
            StormModeActive = true;

            if (Night)
                StormyNight = true;

            return (ScreenState.Up, true);
        }

        if (StormyNight)
            return (ScreenState.Up, true);

        if (windSpeedIsBelowStormThreshold is true or null && rainRateIsBelowStormThreshold is true or null && shortTermRainForecastIsBelowStormThreshold is true or null)
        {
            StormModeActive = false;
            return (null, false);
        }

        if (StormModeActive)
            return (ScreenState.Up, true);

        return (null, false);
    }

    public void Dispose()
    {
        if (WindSpeedSensor != null)
        {
            WindSpeedSensor.WentAboveThreshold -= CheckDesiredState;
            WindSpeedSensor.DroppedBelowThreshold -= CheckDesiredState;
            WindSpeedSensor.Dispose();
        }

        if (RainRateSensor != null)
        {
            RainRateSensor.WentAboveThreshold -= CheckDesiredState;
            RainRateSensor.DroppedBelowThreshold -= CheckDesiredState;
            RainRateSensor.Dispose();
        }

        if (ShortTermRainForecastSensor != null)
        {
            ShortTermRainForecastSensor.WentAboveThreshold -= CheckDesiredState;
            ShortTermRainForecastSensor.DroppedBelowThreshold -= CheckDesiredState;
            ShortTermRainForecastSensor.Dispose();
        }
    }
}