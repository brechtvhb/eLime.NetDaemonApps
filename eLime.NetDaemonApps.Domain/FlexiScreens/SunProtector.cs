using eLime.NetDaemonApps.Domain.Entities.Sun;

namespace eLime.NetDaemonApps.Domain.FlexiScreens;

public class SunProtector
{
    private double ScreenOrientation { get; }
    private Sun Sun { get; }
    private double? OrientationThreshold { get; }
    private double? ElevationThreshold { get; }
    private ScreenState? DesiredStateBelowElevationThreshold { get; }

    public (ScreenState? State, Boolean Enforce) DesiredState { get; private set; }

    public SunProtector(double screenOrientation, Sun sun, double? orientationThreshold, double? elevationThreshold, ScreenState? desiredStateBelowElevationThreshold)
    {
        ScreenOrientation = screenOrientation;
        Sun = sun;
        Sun.StateChanged += (_, _) => CheckDesiredState();
        OrientationThreshold = orientationThreshold;
        ElevationThreshold = elevationThreshold;
        DesiredStateBelowElevationThreshold = desiredStateBelowElevationThreshold;

        CheckDesiredState();
    }

    private void CheckDesiredState()
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

    private (ScreenState? State, Boolean Enforce) GetDesiredState()
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
        if (startOrientationThreshold < 0)
            startOrientationThreshold += 360;

        var endOrientationThreshold = ScreenOrientation + OrientationThreshold;
        if (endOrientationThreshold > 360)
            endOrientationThreshold -= 360;

        var azimuthWithinThreshold = Sun.Attributes.Azimuth > startOrientationThreshold && Sun.Attributes.Azimuth < endOrientationThreshold;

        if (elevationAboveThreshold && azimuthWithinThreshold)
            return (ScreenState.Down, false);

        return (ScreenState.Up, false);
    }
}