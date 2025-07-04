﻿using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.Entities.Weather;
using Microsoft.Extensions.Logging;

namespace eLime.NetDaemonApps.Domain.FlexiScreens;

public class TemperatureProtector : IDisposable
{
    private ILogger Logger { get; }
    private NumericThresholdSensor? SolarLuxSensor { get; }
    public double? SolarLuxAboveThreshold { get; set; }
    public double? SolarLuxBelowThreshold { get; set; }

    private NumericSensor? IndoorTemperatureSensor { get; }
    private Weather? Weather { get; }
    private BinarySensor? IsCoolingSensor { get; }

    public double? MaxIndoorTemperature { get; }
    public double? ConditionalMaxIndoorTemperature { get; }
    public double? ConditionalMaxOutdoorTemperaturePrediction { get; }
    public int? ConditionalOutdoorTemperaturePredictionDays { get; }

    public (ScreenState? State, bool Enforce) DesiredState { get; private set; }

    private bool TemperatureProtectorActive { get; set; }

    public TemperatureProtector(ILogger logger, NumericThresholdSensor? solarLuxSensor, double? solarLuxAboveThreshold, double? solarLuxBelowThreshold,
        NumericSensor? indoorTemperatureSensor, double? maxIndoorTemperature, double? maxConditionalIndoorTemperature,
        Weather? weather, BinarySensor? isCoolingSensor, double? conditionalMaxOutdoorTemperaturePrediction, int? conditionalOutdoorTemperaturePredictionDays)
    {
        Logger = logger;
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

        if (isCoolingSensor != null)
        {
            IsCoolingSensor = isCoolingSensor;
            IsCoolingSensor.TurnedOn += CheckDesiredState;
            IsCoolingSensor.TurnedOff += CheckDesiredState;
        }
    }

    private void CheckDesiredState(object? o, BinarySensorEventArgs sender)
    {
        CheckDesiredState();
    }


    private void CheckDesiredState(object? o, NumericSensorEventArgs sender)
    {
        CheckDesiredState();
    }


    internal void CheckDesiredState(bool emitEvent = true)
    {
        var desiredState = GetDesiredState();

        if (DesiredState == desiredState)
            return;

        DesiredState = desiredState;

        if (!emitEvent)
            return;

        OnDesiredStateChanged(new DesiredStateEventArgs(Protectors.TemperatureProtector, desiredState.State, desiredState.Enforce));
    }

    public event EventHandler<DesiredStateEventArgs>? DesiredStateChanged;

    protected void OnDesiredStateChanged(DesiredStateEventArgs e)
    {
        DesiredStateChanged?.Invoke(this, e);
    }

    public (ScreenState? State, bool Enforce) GetDesiredState()
    {
        double? averagePredictedTemperature = null;

        if (Weather?.Attributes?.Forecast != null && ConditionalOutdoorTemperaturePredictionDays != null)
            averagePredictedTemperature = Weather.Attributes.Forecast.Take(ConditionalOutdoorTemperaturePredictionDays.Value).Average(x => x.Temperature);

        var solarLuxIndicatingSunIsShining = SolarLuxSensor != null && SolarLuxAboveThreshold != null && SolarLuxSensor.State > SolarLuxAboveThreshold;
        var solarLuxIndicatingSunIsNotShining = SolarLuxSensor != null && SolarLuxBelowThreshold != null && SolarLuxSensor.State <= SolarLuxBelowThreshold;

        var isCooling = IsCoolingSensor != null && IsCoolingSensor.IsOn();
        var tooHotInside = IndoorTemperatureSensor != null && MaxIndoorTemperature != null && IndoorTemperatureSensor.State > MaxIndoorTemperature;
        var okInside = IndoorTemperatureSensor != null && MaxIndoorTemperature != null && IndoorTemperatureSensor.State <= MaxIndoorTemperature - 0.5;

        var hotDaysAhead = IndoorTemperatureSensor != null && ConditionalMaxIndoorTemperature != null && averagePredictedTemperature != null && ConditionalMaxOutdoorTemperaturePrediction != null
                           && IndoorTemperatureSensor.State > ConditionalMaxIndoorTemperature && averagePredictedTemperature > ConditionalMaxOutdoorTemperaturePrediction;
        var noHotDaysAhead = IndoorTemperatureSensor != null && ConditionalMaxIndoorTemperature != null && averagePredictedTemperature != null && ConditionalMaxOutdoorTemperaturePrediction != null
                           && IndoorTemperatureSensor.State <= ConditionalMaxIndoorTemperature - 0.3 || averagePredictedTemperature <= ConditionalMaxOutdoorTemperaturePrediction;

        if (solarLuxIndicatingSunIsShining && (tooHotInside || hotDaysAhead || isCooling))
        {
            TemperatureProtectorActive = true;
            return (ScreenState.Down, false);
        }

        if (solarLuxIndicatingSunIsNotShining)
        {
            TemperatureProtectorActive = false;
            return (ScreenState.Up, false);
        }

        if (okInside && noHotDaysAhead)
            TemperatureProtectorActive = false;

        return TemperatureProtectorActive ? (ScreenState.Down, false) : (ScreenState.Up, false);
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

        if (IsCoolingSensor != null)
        {
            IsCoolingSensor.TurnedOn -= CheckDesiredState;
            IsCoolingSensor.TurnedOff -= CheckDesiredState;
            IsCoolingSensor.Dispose();
        }
    }
}