using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.Entities.BinarySensors;

public record BinarySensor : Entity<BinarySensor, EntityState<BinarySensorAttributes>, BinarySensorAttributes>, IDisposable
{
    private IDisposable subscribeDisposable { get; set; }
    public BinarySensor(IHaContext haContext, string entityId) : base(haContext, entityId)
    {
    }

    public BinarySensor(Entity entity) : base(entity)
    {

    }

    public void Initialize()
    {
        subscribeDisposable = StateChanges()
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

    public bool IsOn() => State == "on";

    public void Dispose()
    {
        subscribeDisposable.Dispose();
    }
}