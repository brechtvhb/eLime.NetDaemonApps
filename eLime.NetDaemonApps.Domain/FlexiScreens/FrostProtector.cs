using eLime.NetDaemonApps.Domain.Entities.Weather;

namespace eLime.NetDaemonApps.Domain.FlexiScreens;

public class FrostProtector
{
    public Weather? Weather { get; }


    public FrostProtector(Weather? weather)
    {
        Weather = weather;
    }

    public (ScreenState? State, bool Enforce) GetDesiredState()
    {
        if (Weather?.Attributes?.Temperature < 1)
            return (null, true);

        return (null, false);
    }
}