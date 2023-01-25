using eLime.NetDaemonApps.Domain.Entities.Sun;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.FlexiScreens;

public class DesiredStateEventArgs : EventArgs
{
    public DesiredStateEventArgs(ScreenState? desiredState, bool enforce)
    {
        DesiredState = desiredState;
        Enforce = enforce;
    }

    public ScreenState? DesiredState { get; init; }
    public bool Enforce { get; init; }
    public EntityState<SunAttributes>? Old { get; init; }

}