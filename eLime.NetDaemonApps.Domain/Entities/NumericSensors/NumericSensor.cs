using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.Entities.NumericSensors;

public record NumericSensor : NumericEntity
{
    public NumericSensor(IHaContext haContext, string entityId) : base(haContext, entityId)
    {
    }

    public NumericSensor(Entity entity) : base(entity)
    {
    }


    public void Initialize()
    {
        StateChanges()
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
}