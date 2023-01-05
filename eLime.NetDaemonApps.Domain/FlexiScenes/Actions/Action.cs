namespace eLime.NetDaemonApps.Domain.FlexiScenes.Actions;

public abstract class Action
{
    public abstract Task Execute(bool isAutoTransition = false);
}