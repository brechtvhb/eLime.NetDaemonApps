using Microsoft.Extensions.Logging;

namespace eLime.NetDaemonApps.Domain.FlexiScreens;

public class WomanIsAngryProtector
{
    private ILogger Logger { get; }
    public TimeSpan MinimumIntervalSinceLastManualAction { get; set; }

    public WomanIsAngryProtector(ILogger logger, TimeSpan? minimumIntervalSinceLastManualAction)
    {
        Logger = logger;
        MinimumIntervalSinceLastManualAction = minimumIntervalSinceLastManualAction ?? TimeSpan.FromHours(2);
    }

    public (ScreenState? State, bool Enforce) GetDesiredState(DateTimeOffset now, DateTimeOffset? lastUpdate)
    {
        lastUpdate ??= DateTimeOffset.MinValue;

        if (lastUpdate.Value.Add(MinimumIntervalSinceLastManualAction) > now)
            return (null, true);

        return (null, false);
    }
}