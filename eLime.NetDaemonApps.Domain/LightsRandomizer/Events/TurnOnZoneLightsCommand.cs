using MediatR;

namespace eLime.NetDaemonApps.Domain.LightsRandomizer;

public class TurnOnZoneLightsCommand(string zone, string scene) : INotification
{
    public string Zone => zone;
    public string Scene => scene;
}