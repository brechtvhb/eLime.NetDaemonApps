using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.Entities.NumericSensors;

public record NumericThresholdSensor : NumericEntity
{
    public Double? Threshold { get; private set; }
    public Double? BelowThreshold { get; private set; }
    public NumericThresholdSensor(IHaContext haContext, string entityId) : base(haContext, entityId)
    {
    }

    public NumericThresholdSensor(Entity entity) : base(entity)
    {
    }

    public void Initialize(Double? threshold, Double? belowThreshold = null)
    {
        Threshold = threshold;
        BelowThreshold = belowThreshold ?? threshold;

        StateChanges()
            .Subscribe(x =>
            {
                if (x.Old != null && x.New != null && x.Old.State > BelowThreshold && x.New.State <= BelowThreshold)
                {
                    OnDroppedBelowThreshold(new NumericSensorEventArgs(x));
                }
                if (x.Old != null && x.New != null && x.Old.State < Threshold && x.New.State >= Threshold)
                {
                    OnWentAboveThreshold(new NumericSensorEventArgs(x));
                }
            });
    }

    public static NumericThresholdSensor Create(IHaContext haContext, string entityId, Double? threshold, Double? belowThreshold = null)
    {
        var sensor = new NumericThresholdSensor(haContext, entityId);
        sensor.Initialize(threshold, belowThreshold);
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