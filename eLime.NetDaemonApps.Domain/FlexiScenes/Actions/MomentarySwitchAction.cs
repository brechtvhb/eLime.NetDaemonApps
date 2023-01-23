using eLime.NetDaemonApps.Domain.Scripts;

namespace eLime.NetDaemonApps.Domain.FlexiScenes.Actions;

public class MomentarySwitchAction : Action
{
    public Script Script { get; init; }
    public string EntityId { get; init; }

    public MomentarySwitchAction(Script script, String entityId)
    {
        Script = script;

        EntityId = entityId ?? throw new ArgumentNullException("scriptData.entityId");
    }

    public override Task Execute(Boolean isAutoTransition = false)
    {
        Script.MomentarySwitch(new MomentarySwitchParameters()
        {
            EntityId = EntityId
        });

        return Task.CompletedTask;
    }
}