namespace FlexiLights.Data.Rooms.Actions;

public abstract class Action
{
    public abstract Task Execute(Boolean isAutoTransition = false);
}