namespace eLime.NetDaemonApps.Domain.Rooms.Actions;

public class ExecuteOffActionsAction : Action
{
    public ExecuteOffActionsAction()
    {

    }


    //ExecuteOffActions Domain event?
    public override Task Execute(Boolean isAutoTransition = false)
    {
        return Task.CompletedTask;
    }
}