using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.Entities.NumericSensors;

public record NumericThresholdSensor : NumericEntity, IDisposable
{
    private IDisposable? _subscribeDisposable;
    private IDisposable? _subscribeAboveForDisposable;
    private IDisposable? _subscribeBelowForDisposable;

    public TimeSpan ThresholdTimeSpan { get; private set; }
    public double? Threshold { get; private set; }
    public double? BelowThreshold { get; private set; }
    public NumericThresholdSensor(IHaContext haContext, string entityId) : base(haContext, entityId)
    {
    }

    public NumericThresholdSensor(Entity entity) : base(entity)
    {
    }

    public void Initialize(double? threshold, TimeSpan thresholdTimeSpan, IScheduler? scheduler, double? belowThreshold = null)
    {
        Threshold = threshold;
        BelowThreshold = belowThreshold ?? threshold;
        ThresholdTimeSpan = thresholdTimeSpan;

        if (ThresholdTimeSpan != TimeSpan.Zero && scheduler != null)
        {
            _subscribeBelowForDisposable = StateChanges()
                .WhenStateIsFor(x => x?.State < Threshold, ThresholdTimeSpan, scheduler)
                .Subscribe(x => OnDroppedBelowThreshold(new NumericSensorEventArgs(x.Entity, x.New, x.Old)));

            _subscribeAboveForDisposable = StateChanges()
                .WhenStateIsFor(x => x?.State > Threshold, ThresholdTimeSpan, scheduler)
                .Subscribe(x => OnWentAboveThreshold(new NumericSensorEventArgs(x.Entity, x.New, x.Old)));
        }
        else
        {
            _subscribeDisposable = StateChanges()
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


    }

    public static NumericThresholdSensor Create(IHaContext haContext, string entityId, double? threshold, double? belowThreshold = null)
    {
        var sensor = new NumericThresholdSensor(haContext, entityId);
        sensor.Initialize(threshold, TimeSpan.Zero, null, belowThreshold);
        return sensor;
    }

    public static NumericThresholdSensor Create(IHaContext haContext, string entityId, double? threshold, TimeSpan thresholdTimeSpan, IScheduler scheduler)
    {
        var sensor = new NumericThresholdSensor(haContext, entityId);
        sensor.Initialize(threshold, thresholdTimeSpan, scheduler);
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

    public void Dispose()
    {
        _subscribeDisposable?.Dispose();
        _subscribeAboveForDisposable?.Dispose();
        _subscribeBelowForDisposable?.Dispose();
    }

}