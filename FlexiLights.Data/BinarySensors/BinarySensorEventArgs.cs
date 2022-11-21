using NetDaemon.HassModel.Entities;

namespace FlexiLights.Data.BinarySensors;

public class BinarySensorEventArgs : EventArgs
{
    public BinarySensorEventArgs(StateChange<BinarySensors.BinarySensor, EntityState<BinarySensorAttributes>> stateChange)
    {
        Sensor = stateChange.Entity;
        New = stateChange.New;
        Old = stateChange.Old;
    }

    public BinarySensorEventArgs(BinarySensors.BinarySensor sensor, EntityState<BinarySensorAttributes> @new, EntityState<BinarySensorAttributes>? old)
    {
        Sensor = sensor;
        New = @new;
        Old = old;
    }

    public BinarySensors.BinarySensor Sensor { get; init; }
    public EntityState<BinarySensorAttributes>? New { get; init; }
    public EntityState<BinarySensorAttributes>? Old { get; init; }

}