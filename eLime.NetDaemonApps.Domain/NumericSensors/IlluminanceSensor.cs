using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.NumericSensors;

public record IlluminanceSensor : NumericEntity
{
    public Int32? Threshold { get; private set; }
    public IlluminanceSensor(IHaContext haContext, string entityId) : base(haContext, entityId)
    {
    }

    public IlluminanceSensor(Entity entity) : base(entity)
    {
    }

    public void Initialize(Int32? threshold)
    {
        Threshold = threshold;
        StateChanges()
            .Subscribe(x =>
            {
                if (x.Old != null && x.New != null && x.Old.State >= threshold && x.New.State < threshold)
                {
                    OnDroppedBelowThreshold(new NumericSensorEventArgs(x));
                }
                if (x.Old != null && x.New != null && x.Old.State <= threshold && x.New.State > threshold)
                {
                    OnWentAboveThreshold(new NumericSensorEventArgs(x));
                }
            });
    }

    public static IlluminanceSensor Create(IHaContext haContext, string entityId, Int32? threshold)
    {
        var sensor = new IlluminanceSensor(haContext, entityId);
        sensor.Initialize(threshold);
        return sensor;
    }

    public event EventHandler<NumericSensorEventArgs>? DroppedBelowThreshold;
    public event EventHandler<NumericSensorEventArgs>? WentAboveThreshold;

    private void OnDroppedBelowThreshold(NumericSensorEventArgs e)
    {
        DroppedBelowThreshold?.Invoke(this, e);
    }
    private void OnWentAboveThreshold(NumericSensorEventArgs e)
    {
        WentAboveThreshold?.Invoke(this, e);
    }

}