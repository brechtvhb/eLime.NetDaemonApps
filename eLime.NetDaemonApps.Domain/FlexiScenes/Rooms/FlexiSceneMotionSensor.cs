using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using Action = eLime.NetDaemonApps.Domain.FlexiScenes.Actions.Action;

namespace eLime.NetDaemonApps.Domain.FlexiScenes.Rooms;

public class FlexiSceneMotionSensor
{
    public static FlexiSceneMotionSensor Create(MotionSensor sensor)
    {
        return new FlexiSceneMotionSensor { Sensor = sensor };
    }

    public MotionSensor Sensor { get; private set; }
    public String? MixinScene { get; private set; }
    public DateTimeOffset? TurnOffAt { get; set; }
    internal IDisposable? TurnOffSchedule { get; set; }

    //Should only turn off things that were off before mixin
    public List<Action> ActionsToExecuteOnTurnOff { get; set; } = [];


    public void SetTurnOffAt(DateTimeOffset turnOffAt)
    {
        if (ActionsToExecuteOnTurnOff.Count > 0)
            TurnOffAt = turnOffAt;
    }
    public void ClearTurnOffAt()
    {
        TurnOffAt = null;
        TurnOffSchedule?.Dispose();
        TurnOffSchedule = null;
    }

    public void SetActionsToExecuteOnTurnOff(List<Action> actionsToExecuteOnTurnOff)
    {
        ActionsToExecuteOnTurnOff = actionsToExecuteOnTurnOff;
    }

    public void TurnedOff()
    {
        ClearTurnOffAt();
        ActionsToExecuteOnTurnOff = [];
    }

    public void SetMixinScene(String scene)
    {
        MixinScene = scene;
    }


}
