namespace eLime.NetDaemonApps.Domain.FlexiScreens;

public class ManIsAngryProtector
{
    public TimeSpan MinimumIntervalSinceLastAutomatedAction { get; set; }

    public ManIsAngryProtector(TimeSpan? minimumIntervalSinceLastAutomatedAction)
    {
        MinimumIntervalSinceLastAutomatedAction = minimumIntervalSinceLastAutomatedAction ?? TimeSpan.FromMinutes(15);
    }

    public (ScreenState? State, Boolean Enforce) GetDesiredState(DateTime? lastUpdate)
    {
        lastUpdate ??= DateTime.MinValue;

        if (lastUpdate.Value.Add(MinimumIntervalSinceLastAutomatedAction) > DateTime.Now)
            return (null, true);

        return (null, false);
    }
}