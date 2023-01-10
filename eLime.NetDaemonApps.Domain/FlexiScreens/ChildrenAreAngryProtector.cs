using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;

namespace eLime.NetDaemonApps.Domain.FlexiScreens;

public class ChildrenAreAngryProtector
{
    //Eg: Kids sleeping sensor / operating mode
    public BinarySensor ForceDownSensor { get; }

    public ChildrenAreAngryProtector(BinarySensor forceDownSensor)
    {
        ForceDownSensor = forceDownSensor;
        ForceDownSensor.TurnedOn += CheckStateDesiredState;
        ForceDownSensor.TurnedOff += CheckStateDesiredState;
    }

    public (ScreenState? State, Boolean Enforce) DesiredState { get; private set; }

    private void CheckStateDesiredState(object? sender, BinarySensorEventArgs e)
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
        return ForceDownSensor.State == "on"
            ? (ScreenState.Down, true)
            : (null, false);
    }
}