namespace eLime.NetDaemonApps.Domain.Rooms.Actions;

public abstract class Action
{
    public abstract Task Execute(Boolean isAutoTransition = false);
}