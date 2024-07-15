namespace eLime.NetDaemonApps.Domain.SmartWashers;

internal class SmartWasherFileStorage
{
    public Boolean Enabled { get; set; }
    public Boolean CanDelayStart { get; set; }
    public Boolean DelayedStartTriggered { get; set; }

    public WasherStates State { get; set; }
    public WasherProgram? Program { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? Eta { get; set; }
    public Int32? PercentageComplete { get; set; }
}