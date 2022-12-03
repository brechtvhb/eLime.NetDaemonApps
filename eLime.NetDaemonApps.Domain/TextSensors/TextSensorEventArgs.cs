using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.TextSensors;

public class TextSensorEventArgs : EventArgs
{
    public TextSensorEventArgs(StateChange<TextSensor, EntityState<TextSensorAttributes>> stateChange)
    {
        Sensor = stateChange.Entity;
        New = stateChange.New;
        Old = stateChange.Old;
    }

    public TextSensorEventArgs(TextSensor sensor, EntityState<TextSensorAttributes> @new, EntityState<TextSensorAttributes>? old)
    {
        Sensor = sensor;
        New = @new;
        Old = old;
    }

    public TextSensor Sensor { get; init; }
    public EntityState<TextSensorAttributes>? New { get; init; }
    public EntityState<TextSensorAttributes>? Old { get; init; }

}