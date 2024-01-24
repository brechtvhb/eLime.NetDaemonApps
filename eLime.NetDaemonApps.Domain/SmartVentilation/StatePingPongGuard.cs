using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SmartVentilation;

public class StatePingPongGuard : IDisposable
{
    private readonly ILogger _logger;
    private readonly IScheduler _scheduler;
    public TimeSpan TimeoutSpan { get; }


    public StatePingPongGuard(ILogger logger, IScheduler scheduler, TimeSpan? timeoutSpan)
    {
        _logger = logger;
        _scheduler = scheduler;
        TimeoutSpan = timeoutSpan ?? TimeSpan.FromMinutes(15);
    }

    internal (VentilationState? State, Boolean Enforce) GetDesiredState(VentilationState? currentState, DateTimeOffset? lastChange) =>
        lastChange switch
        {
            null => (null, false),
            _ when lastChange.Value.Add(TimeoutSpan) < _scheduler.Now => (null, false),
            _ => (currentState, true)
        };

    public void Dispose()
    {
    }
}