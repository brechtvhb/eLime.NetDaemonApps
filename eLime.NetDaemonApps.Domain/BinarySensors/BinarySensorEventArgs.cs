using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.BinarySensors;

public class BinarySensorEventArgs : EventArgs
{
    public BinarySensorEventArgs(StateChange<BinarySensor, EntityState<BinarySensorAttributes>> stateChange)
    {
        Sensor = stateChange.Entity;
        New = stateChange.New;
        Old = stateChange.Old;
    }

    public BinarySensorEventArgs(BinarySensor sensor, EntityState<BinarySensorAttributes> @new, EntityState<BinarySensorAttributes>? old)
    {
        Sensor = sensor;
        New = @new;
        Old = old;
    }

    public BinarySensor Sensor { get; init; }
    public EntityState<BinarySensorAttributes>? New { get; init; }
    public EntityState<BinarySensorAttributes>? Old { get; init; }

}