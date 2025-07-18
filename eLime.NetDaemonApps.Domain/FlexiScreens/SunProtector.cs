﻿using eLime.NetDaemonApps.Domain.Entities.Sun;
using Microsoft.Extensions.Logging;

namespace eLime.NetDaemonApps.Domain.FlexiScreens;

public class SunProtector : IDisposable
{
    private ILogger Logger { get; }

    private double ScreenOrientation { get; }
    private Sun Sun { get; }
    private double? OrientationThreshold { get; }
    private double? ElevationThreshold { get; }
    private ScreenState? DesiredStateBelowElevationThreshold { get; }

    public (ScreenState? State, bool Enforce) DesiredState { get; private set; }

    public SunProtector(ILogger logger, double screenOrientation, Sun sun, double? orientationThreshold, double? elevationThreshold, ScreenState? desiredStateBelowElevationThreshold)
    {
        Logger = logger;
        ScreenOrientation = screenOrientation;
        Sun = sun;
        Sun.StateChanged += CheckDesiredState;
        OrientationThreshold = orientationThreshold;
        ElevationThreshold = elevationThreshold;
        DesiredStateBelowElevationThreshold = desiredStateBelowElevationThreshold;
    }

    private void CheckDesiredState(object? o, SunEventArgs sender)
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

        OnDesiredStateChanged(new DesiredStateEventArgs(Protectors.SunProtector, desiredState.State, desiredState.Enforce));
    }

    public event EventHandler<DesiredStateEventArgs>? DesiredStateChanged;

    protected void OnDesiredStateChanged(DesiredStateEventArgs e)
    {
        DesiredStateChanged?.Invoke(this, e);
    }

    private (ScreenState? State, bool Enforce) GetDesiredState()
    {
        var elevationAboveThreshold = false;

        if (Sun.Attributes?.Elevation == null || Sun.Attributes?.Azimuth == null)
            throw new ArgumentException("Sun must have elevation and azimuth attributes");

        if (ElevationThreshold != null)
        {
            if (Sun.Attributes.Elevation <= ElevationThreshold && DesiredStateBelowElevationThreshold != null)
                return (DesiredStateBelowElevationThreshold.Value, true);

            elevationAboveThreshold = Sun.Attributes.Elevation > ElevationThreshold;
        }

        if (OrientationThreshold == null)
            return (null, false);

        var startOrientationThreshold = ScreenOrientation - OrientationThreshold;
        var endOrientationThreshold = ScreenOrientation + OrientationThreshold;
        var azimuthWithinThreshold = Sun.Attributes.Azimuth > startOrientationThreshold && Sun.Attributes.Azimuth < endOrientationThreshold;

        if (elevationAboveThreshold && azimuthWithinThreshold)
            return (ScreenState.Down, false);

        return (ScreenState.Up, false);
    }

    public void Dispose()
    {
        Sun.StateChanged -= CheckDesiredState;
        Sun.Dispose();
    }
}