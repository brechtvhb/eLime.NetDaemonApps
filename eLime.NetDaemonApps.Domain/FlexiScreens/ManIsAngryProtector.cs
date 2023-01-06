namespace eLime.NetDaemonApps.Domain.FlexiScreens;

public class ManIsAngryProtector
{
    public TimeSpan MinimumIntervalSinceLastAutomatedAction { get; set; }

    public ManIsAngryProtector(TimeSpan? minimumIntervalSinceLastAutomatedAction)
    {
        MinimumIntervalSinceLastAutomatedAction = minimumIntervalSinceLastAutomatedAction ?? TimeSpan.FromMinutes(15);
    }

    public (ScreenState? State, Boolean Enforce) GetDesiredState(ScreenState currentScreenState)
    {
        return currentScreenState switch
        {
            ScreenState.Up when DateTime.Now.Add(MinimumIntervalSinceLastAutomatedAction) > DateTime.Now => (ScreenState.Up, true),
            ScreenState.Down when DateTime.Now.Add(MinimumIntervalSinceLastAutomatedAction) > DateTime.Now => (ScreenState.Down, true),
            _ => (null, false)
        };
    }
}