namespace eLime.NetDaemonApps.Domain.FlexiScreens;

internal class FlexiScreenFileStorage
{
    public Boolean Enabled { get; set; }
    public DateTimeOffset? LastAutomatedStateChange { get; set; }
    public DateTimeOffset? LastManualStateChange { get; set; }
    public Protectors? LastStateChangeTriggeredBy { get; set; }
    public Boolean StormyNight { get; set; }

    public bool Equals(FlexiScreenFileStorage r)
    {
        return Enabled == r.Enabled && LastAutomatedStateChange == r.LastAutomatedStateChange && LastManualStateChange == r.LastManualStateChange && LastStateChangeTriggeredBy == r.LastStateChangeTriggeredBy && StormyNight == r.StormyNight;
    }
}