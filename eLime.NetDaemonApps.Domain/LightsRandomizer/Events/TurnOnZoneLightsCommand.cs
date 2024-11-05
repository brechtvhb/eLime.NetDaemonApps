using MediatR;

namespace eLime.NetDaemonApps.Domain.LightsRandomizer;

public class TurnOnZoneLightsCommand(String zone, String scene) : INotification
{
    public string Zone => zone;
    public string Scene => scene;
}