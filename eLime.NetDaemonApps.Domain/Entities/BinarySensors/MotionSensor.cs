using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.Entities.BinarySensors;

public record MotionSensor : BinarySensor
{
    public MotionSensor(IHaContext haContext, string entityId) : base(haContext, entityId)
    {
    }

    public MotionSensor(Entity entity) : base(entity)
    {
    }

    public new static MotionSensor Create(IHaContext haContext, string entityId)
    {
        var sensor = new MotionSensor(haContext, entityId);
        sensor.Initialize();
        return sensor;
    }
}