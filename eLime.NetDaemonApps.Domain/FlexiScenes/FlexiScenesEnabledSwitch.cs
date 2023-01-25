using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.FlexiScenes;

public record FlexiScenesEnabledSwitch : EnabledSwitch<FlexiScenesEnabledSwitchAttributes>

{
    public FlexiScenesEnabledSwitch(IHaContext haContext, string entityId) : base(haContext, entityId)
    {
    }

    public FlexiScenesEnabledSwitch(Entity entity) : base(entity)
    {
    }
}