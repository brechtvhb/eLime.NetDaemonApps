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
        BroadcastAddress = broadcastAddress;
    }

    public override Task<bool?> Execute(bool detectStateChange = false)
    {
        Script.WakeUpPc(new WakeUpPcParameters()
        {
            MacAddress = MacAddress,
            BroadcastAddress = BroadcastAddress,
        });

        bool? initialState = null;
        return Task.FromResult(initialState);
    }

    public override Action Reverse()
    {
        return null;
    }
}