using MediatR;

namespace eLime.NetDaemonApps.Domain.LightsRandomizer.Events;

public class InitLightsRandomizerZoneCommand(string zone, List<string> allowedScenes) : INotification
{
    public string Zone => zone;
    public List<string> AllowedScenes => allowedScenes;
}