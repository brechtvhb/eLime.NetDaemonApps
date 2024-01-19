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


    private (VentilationState? State, Boolean Enforce) GetDesiredState(VentilationState currentState, DateTimeOffset lastChange)
    {
        if (lastChange.Add(TimeoutSpan) < _scheduler.Now)
            return (null, false);

        return (currentState, true);
    }

    public void Dispose()
    {
    }
}