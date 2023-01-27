using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.FlexiScreens;

public record SmartWasherDelayedStartTrigger : EnabledSwitch<EnabledSwitchAttributes>

{
    public SmartWasherDelayedStartTrigger(IHaContext haContext, string entityId) : base(haContext, entityId)
    {
    }

    public SmartWasherDelayedStartTrigger(Entity entity) : base(entity)
    {
    }
}