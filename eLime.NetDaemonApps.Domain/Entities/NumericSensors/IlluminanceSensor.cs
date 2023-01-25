using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.Entities.NumericSensors;

public record IlluminanceSensor : NumericEntity
{
    public Int32? UpperThreshold { get; private set; }
    public Int32? LowerThreshold { get; private set; }
    public IlluminanceSensor(IHaContext haContext, string entityId) : base(haContext, entityId)
    {
    }

    public IlluminanceSensor(Entity entity) : base(entity)
    {
    }

    public void Initialize(Int32? threshold, Int32? lowerThreshold = null)
    {
        UpperThreshold = threshold;
        LowerThreshold = lowerThreshold ?? UpperThreshold;

        StateChanges()
            .Subscribe(x =>
            {
                if (x.Old != null && x.New != null && x.Old.State >= LowerThreshold && x.New.State < LowerThreshold)
                {
                    OnDroppedBelowThreshold(new NumericSensorEventArgs(x));
                }
                if (x.Old != null && x.New != null && x.Old.State <= UpperThreshold && x.New.State > UpperThreshold)
                {
                    OnWentAboveThreshold(new NumericSensorEventArgs(x));
                }
            });
    }

    public static IlluminanceSensor Create(IHaContext haContext, string entityId, Int32? threshold, Int32? lowerThreshold = null)
    {
        var sensor = new IlluminanceSensor(haContext, entityId);
        sensor.Initialize(threshold, lowerThreshold);
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