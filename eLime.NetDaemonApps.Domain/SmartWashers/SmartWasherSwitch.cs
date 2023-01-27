using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.FlexiScreens;

public record SmartWasherSwitch : EnabledSwitch<SmartWasherSwitchAttributes>

{
    public SmartWasherSwitch(IHaContext haContext, string entityId) : base(haContext, entityId)
    {
    }

    public SmartWasherSwitch(Entity entity) : base(entity)
    {
    }
}
