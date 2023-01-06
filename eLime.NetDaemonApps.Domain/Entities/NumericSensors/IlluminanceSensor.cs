using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.Entities.NumericSensors;


public record IlluminanceSensor : NumericThresholdSensor
{
    public IlluminanceSensor(IHaContext haContext, string entityId) : base(haContext, entityId)
    {
    }

    public IlluminanceSensor(Entity entity) : base(entity)
    {
    }
}