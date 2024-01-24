using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.Entities.ClimateEntities;

public class ClimateEventArgs : EventArgs
{
    public ClimateEventArgs(StateChange<Climate, EntityState<ClimateAttributes>> stateChange)
    {
        Sensor = stateChange.Entity;
        New = stateChange.New;
        Old = stateChange.Old;
    }

    public ClimateEventArgs(Climate sensor, EntityState<ClimateAttributes> @new, EntityState<ClimateAttributes>? old)
    {
        Sensor = sensor;
        New = @new;
        Old = old;
    }

    public Climate Sensor { get; init; }
    public EntityState<ClimateAttributes>? New { get; init; }
    public EntityState<ClimateAttributes>? Old { get; init; }

}