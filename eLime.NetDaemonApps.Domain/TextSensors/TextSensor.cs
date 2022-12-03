using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.TextSensors;

public record TextSensor : Entity<TextSensor, EntityState<TextSensorAttributes>, TextSensorAttributes>
{
    public TextSensor(IHaContext haContext, string entityId) : base(haContext, entityId)
    {
    }

    public TextSensor(Entity entity) : base(entity)
    {
    }

}