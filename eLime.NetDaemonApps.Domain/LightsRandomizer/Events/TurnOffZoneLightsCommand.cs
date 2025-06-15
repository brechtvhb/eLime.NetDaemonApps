using MediatR;

namespace eLime.NetDaemonApps.Domain.LightsRandomizer;

public class TurnOffZoneLightsCommand(string zone, string scene) : INotification
{
    public string Zone => zone;
    public string Scene => scene;
}