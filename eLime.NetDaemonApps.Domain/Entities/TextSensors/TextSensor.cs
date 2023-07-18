using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.Entities.TextSensors;

public record TextSensor : Entity<TextSensor, EntityState<TextSensorAttributes>, TextSensorAttributes>, IDisposable
{
    private IDisposable _subscribeDisposable;

    public TextSensor(IHaContext haContext, string entityId) : base(haContext, entityId)
    {
    }

    public TextSensor(Entity entity) : base(entity)
    {
    }

    public void Initialize()
    {

        _subscribeDisposable = StateChanges()
            .Subscribe(x =>
            {
                if (x.New != null)
                {
                    OnStateChanged(new TextSensorEventArgs(x));
                }
            });
    }

    public static TextSensor Create(IHaContext haContext, string entityId)
    {
        var sensor = new TextSensor(haContext, entityId);
        sensor.Initialize();
        return sensor;
    }


    public event EventHandler<TextSensorEventArgs>? StateChanged;

    private void OnStateChanged(TextSensorEventArgs e)
    {
        if (e.New?.State != e.Old?.State)
            StateChanged?.Invoke(this, e);
    }

    public void Dispose()
    {
        _subscribeDisposable?.Dispose();
    }
}