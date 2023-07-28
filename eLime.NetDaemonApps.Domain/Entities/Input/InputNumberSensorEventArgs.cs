using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.Entities.Input;

public class InputNumberSensorEventArgs : EventArgs
{
    public InputNumberSensorEventArgs(NumericStateChange<InputNumberEntity, NumericEntityState<InputNumberAttributes>> stateChange)
    {
        Sensor = stateChange.Entity;
        New = stateChange.New;
        Old = stateChange.Old;
    }

    public InputNumberSensorEventArgs(InputNumberEntity sensor, NumericEntityState<InputNumberAttributes>? @new, NumericEntityState<InputNumberAttributes>? old)
    {
        Sensor = sensor;
        New = @new;
        Old = old;
    }

    public InputNumberEntity Sensor { get; init; }
    public NumericEntityState<InputNumberAttributes>? New { get; init; }
    public NumericEntityState<InputNumberAttributes>? Old { get; init; }

}