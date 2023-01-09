namespace eLime.NetDaemonApps.Domain.FlexiScreens;

public class WomanIsAngryProtector
{
    public TimeSpan MinimumIntervalSinceLastManualAction { get; set; }

    public WomanIsAngryProtector(TimeSpan? minimumIntervalSinceLastManualAction)
    {
        MinimumIntervalSinceLastManualAction = minimumIntervalSinceLastManualAction ?? TimeSpan.FromHours(2);
    }

    public (ScreenState? State, Boolean Enforce) GetDesiredState(DateTime? lastUpdate, ScreenState currentScreenState)
    {
        return currentScreenState switch
        {
            ScreenState.Up when lastUpdate?.Add(MinimumIntervalSinceLastManualAction) > DateTime.Now => (ScreenState.Up, true),
            ScreenState.Down when lastUpdate?.Add(MinimumIntervalSinceLastManualAction) > DateTime.Now => (ScreenState.Down, true),
            _ => (null, false)
        };
    }
}