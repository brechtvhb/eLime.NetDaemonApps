using eLime.NetDaemonApps.Domain.Entities.Scripts;

namespace eLime.NetDaemonApps.Domain.FlexiScenes.Actions;

public class WakeUpPcAction : Action
{
    public Script Script { get; init; }
    public string MacAddress { get; init; }
    public string BroadcastAddress { get; init; }

    public WakeUpPcAction(Script script, string macAddress, string broadcastAddress)
    {
        Script = script;

        MacAddress = macAddress ?? throw new ArgumentNullException("scriptData.macAddress");
        BroadcastAddress = broadcastAddress ?? throw new ArgumentNullException("scriptData.broadcastAddress");
    }

    public override Task Execute(bool isAutoTransition = false)
    {
        Script.WakeUpPc(new WakeUpPcParameters()
        {
            MacAddress = MacAddress,
            BroadcastAddress = BroadcastAddress,
        });

        return Task.CompletedTask;
    }
}