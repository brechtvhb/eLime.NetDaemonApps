using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SmartVentilation;

public class MoldGuard : IDisposable
{
    private readonly ILogger _logger;
    private readonly IScheduler _scheduler;
    private readonly TimeSpan _maxAwayTimespan;
    private readonly TimeSpan _rechargeTimespan;

    public DateTimeOffset? RechargeStartedAt { get; private set; }

    public MoldGuard(ILogger logger, IScheduler scheduler, TimeSpan? maxAwayTimespan, TimeSpan? rechargeTimespan)
    {
        _logger = logger;
        _scheduler = scheduler;
        _maxAwayTimespan = maxAwayTimespan ?? TimeSpan.FromHours(12);
        _rechargeTimespan = rechargeTimespan ?? TimeSpan.FromHours(1);
    }


    public (VentilationState? State, Boolean Enforce) GetDesiredState(VentilationState? currentState, DateTimeOffset? lastChange)
    {
        if (currentState == VentilationState.Off && lastChange?.Add(_maxAwayTimespan) < _scheduler.Now)
            RechargeStartedAt = _scheduler.Now;

        if (RechargeStartedAt == null || RechargeStartedAt.Value.Add(_rechargeTimespan) >= _scheduler.Now)
            return (VentilationState.Low, true);

        RechargeStartedAt = null;
        return (null, false);

    }

    public void Dispose()
    {
    }
}