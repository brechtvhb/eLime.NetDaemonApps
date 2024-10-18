using eLime.NetDaemonApps.Domain.Entities.Scripts;

namespace eLime.NetDaemonApps.Domain.FlexiScenes.Actions;

public class MomentarySwitchAction : Action
{
    public Script Script { get; init; }
    public string EntityId { get; init; }

    public MomentarySwitchAction(Script script, string entityId)
    {
        Script = script;

        EntityId = entityId ?? throw new ArgumentNullException("scriptData.entityId");
    }

    public override Task<bool?> Execute(bool detectStateChange = false)
    {
        Script.MomentarySwitch(new MomentarySwitchParameters()
        {
            EntityId = EntityId
        });

        bool? initialState = null;
        return Task.FromResult(initialState);
    }

    public override Action Reverse()
    {
        return null;
    }
}