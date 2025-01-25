using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.Entities.Buttons;

public record Button : Entity<Button, EntityState<ButtonAttributes>, ButtonAttributes>, IButtonEntityCore
{
    public Button(IHaContext haContext, string entityId) : base(haContext, entityId)
    {
    }

    public Button(IEntityCore entity) : base(entity)
    {
    }

    public void Press()
    {
        CallService("press");
    }
}