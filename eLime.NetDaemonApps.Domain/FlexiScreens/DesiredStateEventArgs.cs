namespace eLime.NetDaemonApps.Domain.FlexiScreens;

public class DesiredStateEventArgs : EventArgs
{
    public DesiredStateEventArgs(Protectors protector, ScreenState? desiredState, bool enforce)
    {
        Protector = protector;
        DesiredState = desiredState;
        Enforce = enforce;
    }

    public Protectors Protector { get; init; }
    public ScreenState? DesiredState { get; init; }
    public bool Enforce { get; init; }

}