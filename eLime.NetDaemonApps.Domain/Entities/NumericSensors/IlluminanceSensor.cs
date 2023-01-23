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

    public new static IlluminanceSensor Create(IHaContext haContext, string entityId, Double? threshold, Double? belowThreshold = null)
    {
        var sensor = new IlluminanceSensor(haContext, entityId);
        sensor.Initialize(threshold, belowThreshold);
        return sensor;
    }

}