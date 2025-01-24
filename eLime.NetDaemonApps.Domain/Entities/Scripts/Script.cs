using NetDaemon.HassModel;

namespace eLime.NetDaemonApps.Domain.Entities.Scripts;

public class Script(IHaContext haContext)
{
    ///<summary>Activate the scene.</summary>
    public void MomentarySwitch(MomentarySwitchParameters data)
    {
        haContext.CallService("script", "momentary_switch", null, data);
    }
    public void WakeUpPc(WakeUpPcParameters data)
    {
        haContext.CallService("script", "wake_up_pc", null, data);
    }
}