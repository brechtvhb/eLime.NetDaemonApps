using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.Entities.Covers;

public class CoverEventArgs : EventArgs
{
    public CoverEventArgs(StateChange<Cover, EntityState<CoverAttributes>> stateChange)
    {
        Sensor = stateChange.Entity;
        New = stateChange.New;
        Old = stateChange.Old;
    }

    public CoverEventArgs(Cover sensor, EntityState<CoverAttributes> @new, EntityState<CoverAttributes>? old)
    {
        Sensor = sensor;
        New = @new;
        Old = old;
    }

    public Cover Sensor { get; init; }
    public EntityState<CoverAttributes>? New { get; init; }
    public EntityState<CoverAttributes>? Old { get; init; }

}