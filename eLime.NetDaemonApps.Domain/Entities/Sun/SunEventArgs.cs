using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.Entities.Sun;

public class SunEventArgs : EventArgs
{
    public SunEventArgs(StateChange<Sun, EntityState<SunAttributes>> stateChange)
    {
        Sensor = stateChange.Entity;
        New = stateChange.New;
        Old = stateChange.Old;
    }

    public SunEventArgs(Sun sensor, EntityState<SunAttributes> @new, EntityState<SunAttributes>? old)
    {
        Sensor = sensor;
        New = @new;
        Old = old;
    }

    public Sun Sensor { get; init; }
    public EntityState<SunAttributes>? New { get; init; }
    public EntityState<SunAttributes>? Old { get; init; }

}