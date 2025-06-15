namespace eLime.NetDaemonApps.Domain.SmartWashers;

internal class SmartWasherFileStorage
{
    public bool Enabled { get; set; }
    public bool CanDelayStart { get; set; }
    public bool DelayedStartTriggered { get; set; }

    public WasherStates State { get; set; }
    public WasherProgram? Program { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? Eta { get; set; }
    public int? PercentageComplete { get; set; }
}