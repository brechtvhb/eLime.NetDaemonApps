namespace eLime.NetDaemonApps.Domain.FlexiScenes.Actions;

public abstract class Action
{
    public abstract Task<bool?> Execute(bool detectStateChange = false);

    public abstract Action Reverse();
}