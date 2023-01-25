using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.FlexiScreens;
using eLime.NetDaemonApps.Domain.SmartWashers.States;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.HassModel;
using System.Diagnostics;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SmartWashers
{
    public class SmartWasher
    {
        public string? Name { get; }
        private FlexiScreenEnabledSwitch EnabledSwitch { get; set; }

        //TODO: Should make smart switch turn of when it senses power usage and defer until a more optimal moment
        //private Switch DelayedStart { get; set; }
        public NumericSensor PowerSensor { get; set; }
        public Switch Socket { get; set; }
        private readonly IHaContext _haContext;
        private readonly ILogger _logger;
        private readonly NumericSensor _powerSensor;
        private readonly IScheduler _scheduler;
        private readonly IMqttEntityManager _mqttEntityManager;

        private SmartWasherState _state;

        public DateTime LastStateChange;
        public DateTime? Eta { get; set; }
        public WasherProgram? Program { get; set; }

        public WasherStates State => _state switch
        {
            IdleState => WasherStates.Idle,
            PreWashingState => WasherStates.PreWashing,
            HeatingState => WasherStates.Heating,
            _ => throw new ArgumentOutOfRangeException(nameof(_state))
        };

        //TODO: create brol
        public SmartWasher(ILogger logger, NumericSensor powerSensor)
        {
            _logger = logger;
            _powerSensor = powerSensor;

            //Actually get current state from Home assistant when starting.
            _state = new IdleState();
            _state.Enter(_logger, this);

            PowerSensor.Changed += (_, e) => PowerSensor_Changed(e);
        }

        private void PowerSensor_Changed(NumericSensorEventArgs e)
        {
            _state.PowerUsageChanged(_logger, this);
        }


        internal void TransitionTo(ILogger logger, SmartWasherState state)
        {
            logger.LogDebug($"SmartWasher: Transitioning from state {_state.GetType().Name} to {state.GetType().Name}");
            LastStateChange = DateTime.Now;
            _state = state;
            _state.Enter(logger, this);
            Eta = _state.GetEta(_logger, this);
        }

        internal void SetWasherProgram(WasherProgram? program)
        {
            Program = program;
            Eta = _state.GetEta(_logger, this);
        }
    }

    public enum WasherStates
    {
        Idle,
        DelayedStart,
        PreWashing,
        Heating,
        Washing,
        Rinsing,
        Spinning,
        Ready
    }

    public enum WasherProgram
    {
        Unknown,
        Wash40Degrees,
        Wash60Degrees,
    }
}
