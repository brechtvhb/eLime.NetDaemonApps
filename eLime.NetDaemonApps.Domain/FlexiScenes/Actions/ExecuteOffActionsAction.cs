namespace eLime.NetDaemonApps.Domain.FlexiScenes.Actions;

public class ExecuteOffActionsAction : Action
{
    public ExecuteOffActionsAction()
    {

    }


    //ExecuteOffActions Domain event?
    public override Task Execute(bool isAutoTransition = false)
    {
        return Task.CompletedTask;
    }
}