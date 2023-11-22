using eLime.NetDaemonApps.Domain.FlexiScenes.Rooms;

namespace eLime.NetDaemonApps.Domain.FlexiScenes;

internal class FlexiSceneFileStorage
{
    public Boolean Enabled { get; set; }
    public DateTimeOffset? IgnorePresenceUntil { get; set; }
    public DateTimeOffset? TurnOffAt { get; set; }
    public InitiatedBy? InitiatedBy { get; set; }
    public String? InitialFlexiScene { get; set; }
    public String? ActiveFlexiScene { get; set; }

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