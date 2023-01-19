using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.TextSensors;

public record TextSensor : Entity<TextSensor, EntityState<TextSensorAttributes>, TextSensorAttributes>
{
    public TextSensor(IHaContext haContext, string entityId) : base(haContext, entityId)
    {
    }

    public TextSensor(Entity entity) : base(entity)
    {
    }

    public void Initialize()
    {

        StateChanges()
            .Subscribe(x =>
            {
                if (x.New != null)
                {
                    OnStateChanged(new TextSensorEventArgs(x));
                }
            });
    }

    public event EventHandler<TextSensorEventArgs>? StateChanged;

    private void OnStateChanged(TextSensorEventArgs e)
    {
        if (e.New?.State != e.Old?.State)
            StateChanged?.Invoke(this, e);
    }
}