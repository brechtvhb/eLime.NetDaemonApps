using NetDaemon.HassModel.Entities;

namespace FlexiLights.Data.Numeric;

public class NumericSensorEventArgs : EventArgs
{
    public NumericSensorEventArgs(NumericStateChange stateChange)
    {
        Sensor = stateChange.Entity;
        New = stateChange.New;
        Old = stateChange.Old;
    }

    public NumericSensorEventArgs(NumericEntity sensor, NumericEntityState? @new, NumericEntityState? old)
    {
        Sensor = sensor;
        New = @new;
        Old = old;
    }

    public NumericEntity Sensor { get; init; }
    public NumericEntityState? New { get; init; }
    public NumericEntityState? Old { get; init; }

}