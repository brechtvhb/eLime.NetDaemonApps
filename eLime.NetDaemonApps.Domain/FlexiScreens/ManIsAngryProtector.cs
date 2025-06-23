using Microsoft.Extensions.Logging;

namespace eLime.NetDaemonApps.Domain.FlexiScreens;

public class ManIsAngryProtector
{
    private ILogger Logger { get; }
    public TimeSpan MinimumIntervalSinceLastAutomatedAction { get; set; }

    public ManIsAngryProtector(ILogger logger, TimeSpan? minimumIntervalSinceLastAutomatedAction)
    {
        Logger = logger;
        MinimumIntervalSinceLastAutomatedAction = minimumIntervalSinceLastAutomatedAction ?? TimeSpan.FromMinutes(15);
    }

    public (ScreenState? State, bool Enforce) GetDesiredState(DateTimeOffset now, DateTimeOffset? lastUpdate)
    {
        lastUpdate ??= DateTimeOffset.MinValue;

        if (lastUpdate.Value.Add(MinimumIntervalSinceLastAutomatedAction) > now)
            return (null, true);

        return (null, false);
    }
}