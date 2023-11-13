namespace eLime.NetDaemonApps.Domain.FlexiScreens;

public class ManIsAngryProtector
{
    public TimeSpan MinimumIntervalSinceLastAutomatedAction { get; set; }

    public ManIsAngryProtector(TimeSpan? minimumIntervalSinceLastAutomatedAction)
    {
        MinimumIntervalSinceLastAutomatedAction = minimumIntervalSinceLastAutomatedAction ?? TimeSpan.FromMinutes(15);
    }

    public (ScreenState? State, Boolean Enforce) GetDesiredState(DateTimeOffset now, DateTimeOffset? lastUpdate)
    {
        lastUpdate ??= DateTimeOffset.MinValue;

        if (lastUpdate.Value.Add(MinimumIntervalSinceLastAutomatedAction) > now)
            return (null, true);

        return (null, false);
    }
}