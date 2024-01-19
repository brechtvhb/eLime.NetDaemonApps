namespace eLime.NetDaemonApps.Domain.SmartVentilation;

public class DesiredStateEventArgs : EventArgs
{
    public DesiredStateEventArgs(VentilationGuards guard, VentilationState? desiredState, bool enforce)
    {
        Guard = guard;
        DesiredState = desiredState;
        Enforce = enforce;
    }

    public VentilationGuards Guard { get; init; }
    public VentilationState? DesiredState { get; init; }
    public bool Enforce { get; init; }

}