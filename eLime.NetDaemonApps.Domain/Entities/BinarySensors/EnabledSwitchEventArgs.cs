using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.Entities.BinarySensors;

public class EnabledSwitchEventArgs<T> : EventArgs
    where T : EnabledSwitchAttributes
{
    public EnabledSwitchEventArgs(StateChange<EnabledSwitch<T>, EntityState<T>> stateChange)
    {
        Sensor = stateChange.Entity;
        New = stateChange.New;
        Old = stateChange.Old;
    }

    public EnabledSwitchEventArgs(EnabledSwitch<T> sensor, EntityState<T> @new, EntityState<T>? old)
    {
        Sensor = sensor;
        New = @new;
        Old = old;
    }

    public EnabledSwitch<T> Sensor { get; init; }
    public EntityState<T>? New { get; init; }
    public EntityState<T>? Old { get; init; }

}