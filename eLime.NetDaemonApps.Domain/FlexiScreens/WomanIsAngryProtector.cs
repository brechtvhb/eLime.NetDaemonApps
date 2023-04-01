namespace eLime.NetDaemonApps.Domain.FlexiScreens;

public class WomanIsAngryProtector
{
    public TimeSpan MinimumIntervalSinceLastManualAction { get; set; }

    public WomanIsAngryProtector(TimeSpan? minimumIntervalSinceLastManualAction)
    {
        MinimumIntervalSinceLastManualAction = minimumIntervalSinceLastManualAction ?? TimeSpan.FromHours(2);
    }

    public (ScreenState? State, Boolean Enforce) GetDesiredState(DateTimeOffset now, DateTimeOffset? lastUpdate)
    {
        lastUpdate ??= DateTimeOffset.MinValue;

        if (lastUpdate.Value.Add(MinimumIntervalSinceLastManualAction) > now)
            return (null, true);

        return (null, false);
    }
}