using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.NumericSensors;

public record NumericSensor : NumericEntity<NumericSensor, NumericEntityState<NumericSensorAttributes>, NumericSensorAttributes>
{
    public NumericSensor(IHaContext haContext, string entityId) : base(haContext, entityId)
    {
    }

    public NumericSensor(Entity entity) : base(entity)
    {
    }

}