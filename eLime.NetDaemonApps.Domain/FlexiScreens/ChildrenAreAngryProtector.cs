using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using Microsoft.Extensions.Logging;

namespace eLime.NetDaemonApps.Domain.FlexiScreens;

public class ChildrenAreAngryProtector : IDisposable
{
    private ILogger Logger { get; }
    //Eg: Kids sleeping sensor / operating mode
    public BinarySensor ForceDownSensor { get; }
    public (ScreenState? State, bool Enforce) DesiredState { get; private set; }

    public ChildrenAreAngryProtector(ILogger logger, BinarySensor forceDownSensor)
    {
        Logger = logger;
        ForceDownSensor = forceDownSensor;
        ForceDownSensor.TurnedOn += CheckDesiredState;
        ForceDownSensor.TurnedOff += CheckDesiredState;
    }

    private void CheckDesiredState(object? o, BinarySensorEventArgs sender)
    {
        if (sender.Old?.State != "unavailable")
            CheckDesiredState();
    }

    internal void CheckDesiredState(bool emitEvent = true)
    {
        if (ForceDownSensor.IsOn())
            OnNightStarted(EventArgs.Empty);

        if (ForceDownSensor.IsOff())
            OnNightEnded(EventArgs.Empty);

        var desiredState = GetDesiredState();

        if (DesiredState == desiredState)
            return;

        DesiredState = desiredState;

        if (!emitEvent)
            return;

        OnDesiredStateChanged(new DesiredStateEventArgs(Protectors.ChildrenAreAngryProtector, desiredState.State, desiredState.Enforce));
    }


    public event EventHandler<EventArgs>? NightStarted;

    protected void OnNightStarted(EventArgs e)
    {
        NightStarted?.Invoke(this, e);
    }

    public event EventHandler<EventArgs>? NightEnded;

    protected void OnNightEnded(EventArgs e)
    {
        NightEnded?.Invoke(this, e);
    }


    public event EventHandler<DesiredStateEventArgs>? DesiredStateChanged;

    protected void OnDesiredStateChanged(DesiredStateEventArgs e)
    {
        DesiredStateChanged?.Invoke(this, e);
    }

    public (ScreenState? State, bool Enforce) GetDesiredState()
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