using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.FlexiScreens;

public record SmartWasherDelayedStartSwitch : EnabledSwitch<EnabledSwitchAttributes>

{
    public SmartWasherDelayedStartSwitch(IHaContext haContext, string entityId) : base(haContext, entityId)
    {
    }

    public SmartWasherDelayedStartSwitch(Entity entity) : base(entity)
    {
    }
}
