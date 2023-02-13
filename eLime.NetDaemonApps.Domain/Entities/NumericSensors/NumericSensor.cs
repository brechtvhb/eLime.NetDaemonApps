using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.Entities.NumericSensors;

public record NumericSensor : NumericEntity, IDisposable
{
    private IDisposable _subscribeDisposable;

    public NumericSensor(IHaContext haContext, string entityId) : base(haContext, entityId)
    {
    }

    public NumericSensor(Entity entity) : base(entity)
    {
    }


    public void Initialize()
    {
        _subscribeDisposable = StateChanges()
            .Subscribe(x =>
            {
                OnChanged(new NumericSensorEventArgs(x));
            });
    }

    public static NumericSensor Create(IHaContext haContext, string entityId)
    {
        var sensor = new NumericSensor(haContext, entityId);
        sensor.Initialize();
        return sensor;
    }

    public event EventHandler<NumericSensorEventArgs>? Changed;
    private void OnChanged(NumericSensorEventArgs e)
    {
        Changed?.Invoke(this, e);
    }

    public void Dispose()
    {
        _subscribeDisposable?.Dispose();
    }
}