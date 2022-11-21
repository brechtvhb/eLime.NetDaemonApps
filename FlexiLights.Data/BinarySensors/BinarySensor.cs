using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace FlexiLights.Data.BinarySensors;

public record BinarySensor : Entity<BinarySensor, EntityState<BinarySensorAttributes>, BinarySensorAttributes>
{
    public BinarySensor(IHaContext haContext, string entityId) : base(haContext, entityId)
    {
    }

    public BinarySensor(Entity entity) : base(entity)
    {

    }

    public void Initialize()
    {
        StateChanges()
            .Subscribe(x =>
            {
                if (x.New != null && x.New.IsOn())
                {
                    OnTurnedOn(new BinarySensorEventArgs(x));
                }
                if (x.New != null && x.New.IsOff())
                {
                    OnTurnedOff(new BinarySensorEventArgs(x));
                }
            });
    }

    public static BinarySensor Create(IHaContext haContext, string entityId)
    {
        var sensor = new BinarySensor(haContext, entityId);
        sensor.Initialize();
        return sensor;
    }


    public event EventHandler<BinarySensorEventArgs>? TurnedOn;
    public event EventHandler<BinarySensorEventArgs>? TurnedOff;

    protected void OnTurnedOn(BinarySensorEventArgs e)
    {
        TurnedOn?.Invoke(this, e);
    }
    protected void OnTurnedOff(BinarySensorEventArgs e)
    {
        TurnedOff?.Invoke(this, e);
    }
}