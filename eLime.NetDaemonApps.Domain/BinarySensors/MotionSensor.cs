using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.BinarySensors;

public record MotionSensor : BinarySensor
{
    public MotionSensor(IHaContext haContext, string entityId) : base(haContext, entityId)
    {
    }

    public MotionSensor(Entity entity) : base(entity)
    {
    }

    public new void Initialize()
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

    public new static MotionSensor Create(IHaContext haContext, string entityId)
    {
        var sensor = new MotionSensor(haContext, entityId);
        sensor.Initialize();
        return sensor;
    }

    public new event EventHandler<BinarySensorEventArgs>? TurnedOn;
    public new event EventHandler<BinarySensorEventArgs>? TurnedOff;

    private new void OnTurnedOn(BinarySensorEventArgs e)
    {
        TurnedOn?.Invoke(this, e);
    }
    private new void OnTurnedOff(BinarySensorEventArgs e)
    {
        TurnedOff?.Invoke(this, e);
    }
}