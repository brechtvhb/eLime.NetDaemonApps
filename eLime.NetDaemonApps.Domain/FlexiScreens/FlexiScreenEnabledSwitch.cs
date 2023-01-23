using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.FlexiScreens;

public record FlexiScreenEnabledSwitch : EnabledSwitch<FlexiScreenEnabledSwitchAttributes>

{
    public FlexiScreenEnabledSwitch(IHaContext haContext, string entityId) : base(haContext, entityId)
    {
    }

    public FlexiScreenEnabledSwitch(Entity entity) : base(entity)
    {
    }
}