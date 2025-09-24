using eLime.NetDaemonApps.Domain.Storage;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.HassModel;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SmartHeatPump;

public class SmartHeatPumpContext(IHaContext haContext, ILogger logger, IScheduler scheduler, IFileStorage fileStorage, IMqttEntityManager mqttEntityManager, TimeSpan debounceDuration)
{
    public IHaContext HaContext { get; private init; } = haContext;
    public ILogger Logger { get; private init; } = logger;
    public IScheduler Scheduler { get; private init; } = scheduler;
    public IFileStorage FileStorage { get; private init; } = fileStorage;
    public IMqttEntityManager MqttEntityManager { get; private init; } = mqttEntityManager;
    public TimeSpan DebounceDuration { get; private init; } = debounceDuration;
}