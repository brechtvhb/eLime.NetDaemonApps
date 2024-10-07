using eLime.NetDaemonApps.Domain.Entities.BinarySensors;

namespace eLime.NetDaemonApps.Domain.FlexiScenes.Rooms
{
    public class FlexiSceneMotionSensor
    {
        public static FlexiSceneMotionSensor Create(MotionSensor sensor, String mixinScene)
        {
            return new FlexiSceneMotionSensor { Sensor = sensor, MixinScene = mixinScene };
        }

        public void SetTurnOffAt(DateTime? turnOffAt, List<String> thingsToTurnOff)
        {
            TurnOffAt = turnOffAt;
            ThingsToTurnOff = thingsToTurnOff;
            //Add handler to actually turn off?

        }

        public MotionSensor Sensor { get; private set; }
        public String MixinScene { get; private set; }
        public DateTime? TurnOffAt { get; set; }
        //Should only turn off things that were off before mixin
        public List<String> ThingsToTurnOff { get; set; } = [];
    }
}
