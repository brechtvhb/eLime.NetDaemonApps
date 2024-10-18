namespace eLime.NetDaemonApps.Domain.FlexiScenes.Actions;

public class ExecuteOffActionsAction : Action
{
    //ExecuteOffActions Domain event?
    public override Task<bool?> Execute(bool detectStateChange = false)
    {
        bool? initialState = null;
        return Task.FromResult(initialState);
    }

    public override Action Reverse()
    {
        return null;
    }
}