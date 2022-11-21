using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace FlexiLights.Data.Numeric;

public record NumericSensor : NumericEntity<NumericSensor, NumericEntityState<NumericSensorAttributes>, NumericSensorAttributes>
{
    public NumericSensor(IHaContext haContext, string entityId) : base(haContext, entityId)
    {
    }

    public NumericSensor(Entity entity) : base(entity)
    {
    }

}