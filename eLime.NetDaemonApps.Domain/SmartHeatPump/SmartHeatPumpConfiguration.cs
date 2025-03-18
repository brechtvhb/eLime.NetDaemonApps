using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Storage;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.HassModel;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SmartHeatPump;

public class SmartHeatPumpConfiguration
{
    public required IHaContext HaContext { get; set; }
    public required ILogger Logger { get; set; }
    public required IScheduler Scheduler { get; set; }
    public required IFileStorage FileStorage { get; set; }
    public required IMqttEntityManager MqttEntityManager { get; set; }

    public required BinarySwitch SmartGridReadyInput1 { get; set; }
    public required BinarySwitch SmartGridReadyInput2 { get; set; }

    public TimeSpan DebounceDuration { get; set; }
}