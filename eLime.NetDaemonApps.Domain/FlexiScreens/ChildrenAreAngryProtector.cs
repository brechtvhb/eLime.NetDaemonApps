using eLime.NetDaemonApps.Domain.Entities.BinarySensors;

namespace eLime.NetDaemonApps.Domain.FlexiScreens;

public class ChildrenAreAngryProtector : IDisposable
{
    //Eg: Kids sleeping sensor / operating mode
    public BinarySensor ForceDownSensor { get; }
    public (ScreenState? State, Boolean Enforce) DesiredState { get; private set; }

    public ChildrenAreAngryProtector(BinarySensor forceDownSensor)
    {
        ForceDownSensor = forceDownSensor;
        ForceDownSensor.TurnedOn += CheckDesiredState;
        ForceDownSensor.TurnedOff += CheckDesiredState;

        CheckDesiredState();
    }

    private void CheckDesiredState(Object? o, BinarySensorEventArgs sender)
    {
        CheckDesiredState();
    }

    private void CheckDesiredState()
    {
        var desiredState = GetDesiredState();

        if (DesiredState == desiredState)
            return;

        DesiredState = desiredState;
        OnDesiredStateChanged(new DesiredStateEventArgs(Protectors.ChildrenAreAngryProtector, desiredState.State, desiredState.Enforce));
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

    public void Dispose()
    {
        ForceDownSensor.TurnedOn -= CheckDesiredState;
        ForceDownSensor.TurnedOff -= CheckDesiredState;

        ForceDownSensor.Dispose();
    }
}