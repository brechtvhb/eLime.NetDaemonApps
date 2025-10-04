using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.Entities.Select;

public class SelectEntityEventArgs : EventArgs
{
    public SelectEntityEventArgs(StateChange<SelectEntity, EntityState<SelectEntityAttribute>> stateChange)
    {
        Sensor = stateChange.Entity;
        New = stateChange.New;
        Old = stateChange.Old;
    }

    public SelectEntityEventArgs(SelectEntity sensor, EntityState<SelectEntityAttribute>? @new, EntityState<SelectEntityAttribute>? old)
    {
        Sensor = sensor;
        New = @new;
        Old = old;
    }

    public SelectEntity Sensor { get; init; }
    public EntityState<SelectEntityAttribute>? New { get; init; }
    public EntityState<SelectEntityAttribute>? Old { get; init; }

}