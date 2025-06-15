namespace eLime.NetDaemonApps.Domain.FlexiScreens;

internal class FlexiScreenFileStorage
{
    public bool Enabled { get; set; }
    public DateTimeOffset? LastAutomatedStateChange { get; set; }
    public DateTimeOffset? LastManualStateChange { get; set; }
    public Protectors? LastStateChangeTriggeredBy { get; set; }
    public bool StormyNight { get; set; }

    public bool Equals(FlexiScreenFileStorage? r)
    {
        if (r == null)
            return false;

        return Enabled == r.Enabled && LastAutomatedStateChange == r.LastAutomatedStateChange && LastManualStateChange == r.LastManualStateChange && LastStateChangeTriggeredBy == r.LastStateChangeTriggeredBy && StormyNight == r.StormyNight;
    }
}