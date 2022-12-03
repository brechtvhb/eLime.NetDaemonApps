using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain;

public class SwitchEventArgs : EventArgs
{
    public SwitchEventArgs(Entity sensor)
    {
        Sensor = sensor;
    }
    public Entity Sensor { get; init; }

}