using eLime.NetDaemonApps.Domain.Scripts;
using Action = eLime.NetDaemonApps.Domain.FlexiScenes.Actions.Action;

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

    public override Task Execute(bool isAutoTransition = false)
    {
        Script.MomentarySwitch(new MomentarySwitchParameters()
        {
            EntityId = EntityId
        });

        return Task.CompletedTask;
    }
}

public class WakeUpPcAction : Action
{
    public Script Script { get; init; }
    public string MacAddress { get; init; }

    public WakeUpPcAction(Script script, string macAddress)
    {
        Script = script;

        MacAddress = macAddress ?? throw new ArgumentNullException("scriptData.macAddress");
    }

    public override Task Execute(bool isAutoTransition = false)
    {
        Script.WakeUpPc(new WakeUpPcParameters()
        {
            MacAddress = MacAddress
        });

        return Task.CompletedTask;
    }
}