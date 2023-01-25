using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.Entities.BinarySensors;

public class EnabledSwitchEventArgs : EventArgs
{
    public EnabledSwitchEventArgs(StateChange<EnabledSwitch, EntityState<EnabledSwitchAttributes>> stateChange)
    {
        Sensor = stateChange.Entity;
        New = stateChange.New;
        Old = stateChange.Old;
    }

    public EnabledSwitchEventArgs(EnabledSwitch sensor, EntityState<EnabledSwitchAttributes> @new, EntityState<EnabledSwitchAttributes>? old)
    {
        Sensor = sensor;
        New = @new;
        Old = old;
    }

    public EnabledSwitch Sensor { get; init; }
    public EntityState<EnabledSwitchAttributes>? New { get; init; }
    public EntityState<EnabledSwitchAttributes>? Old { get; init; }

}