using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.Entities.Input;

public record InputNumberEntity : NumericEntity<InputNumberEntity, NumericEntityState<InputNumberAttributes>, InputNumberAttributes>
{
    private IDisposable _subscribeDisposable;

    public InputNumberEntity(IHaContext haContext, string entityId) : base(haContext, entityId)
    {
    }

    public InputNumberEntity(Entity entity) : base(entity)
    {
    }

    public void Initialize()
    {
        _subscribeDisposable = StateChanges()
            .Subscribe(x =>
            {
                OnChanged(new InputNumberSensorEventArgs(x));
            });
    }

    public static InputNumberEntity Create(IHaContext haContext, string entityId)
    {
        var sensor = new InputNumberEntity(haContext, entityId);
        sensor.Initialize();
        return sensor;
    }

    public void Change(Double value)
    {
        CallService("set_value", new InputNumberSetValueParameters { Value = value });
    }

    public event EventHandler<InputNumberSensorEventArgs>? Changed;
    private void OnChanged(InputNumberSensorEventArgs e)
    {
        Changed?.Invoke(this, e);
    }

    public void Dispose()
    {
        _subscribeDisposable?.Dispose();
    }

}