using eLime.NetDaemonApps.Domain.Entities.BinarySensors;

namespace eLime.NetDaemonApps.Domain.FlexiScreens;

public class ChildrenAreAngryProtector
{
    //Eg: Kids sleeping sensor / operating mode
    public BinarySensor ForceDownSensor { get; }

    public ChildrenAreAngryProtector(BinarySensor forceDownSensor)
    {
        ForceDownSensor = forceDownSensor;
    }

    public (ScreenState? State, Boolean Enforce) GetDesiredState(ScreenState currentScreenState)
    {
        return ForceDownSensor.State == "on"
            ? (ScreenState.Down, true)
            : (null, false);
    }
}