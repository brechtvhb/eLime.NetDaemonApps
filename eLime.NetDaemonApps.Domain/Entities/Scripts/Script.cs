using NetDaemon.HassModel;

namespace eLime.NetDaemonApps.Domain.Entities.Scripts;

public class Script
{
    private readonly IHaContext _haContext;
    public Script(IHaContext haContext)
    {
        _haContext = haContext;
    }

    ///<summary>Activate the scene.</summary>
    public void MomentarySwitch(MomentarySwitchParameters data)
    {
        _haContext.CallService("script", "momentary_switch", null, data);
    }
    public void WakeUpPc(WakeUpPcParameters data)
    {
        _haContext.CallService("script", "wake_up_pc", null, data);
    }
}