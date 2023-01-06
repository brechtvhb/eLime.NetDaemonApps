using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.Entities.Sun;

public record Sun : Entity<Sun, EntityState<SunAttributes>, SunAttributes>
{
    public Sun(IHaContext haContext, string entityId) : base(haContext, entityId)
    {
    }

    public Sun(Entity entity) : base(entity)
    {
    }
}
