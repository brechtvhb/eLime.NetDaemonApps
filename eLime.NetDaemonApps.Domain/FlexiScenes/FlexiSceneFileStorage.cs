using eLime.NetDaemonApps.Domain.FlexiScenes.Rooms;

namespace eLime.NetDaemonApps.Domain.FlexiScenes;

internal class FlexiSceneFileStorage
{
    public bool Enabled { get; set; }
    public DateTimeOffset? IgnorePresenceUntil { get; set; }
    public DateTimeOffset? TurnOffAt { get; set; }
    public InitiatedBy? InitiatedBy { get; set; }
    public string? InitialFlexiScene { get; set; }
    public string? ActiveFlexiScene { get; set; }

    public List<FlexiSceneChange> Changes { get; set; } = [];

    public bool Equals(FlexiSceneFileStorage? r)
    {
        if (r == null)
            return false;

        return Enabled == r.Enabled
               && IgnorePresenceUntil == r.IgnorePresenceUntil
               && TurnOffAt == r.TurnOffAt
               && InitiatedBy == r.InitiatedBy
               && InitialFlexiScene == r.InitialFlexiScene
               && ActiveFlexiScene == r.ActiveFlexiScene;
    }
}