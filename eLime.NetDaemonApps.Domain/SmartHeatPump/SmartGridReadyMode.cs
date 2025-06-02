namespace eLime.NetDaemonApps.Domain.SmartHeatPump;

public enum SmartGridReadyMode
{
    Blocked,
    Normal,
    Boosted, //Increases temperatures
    Maximized //Overrules standstill? Nope, just increases temperatures to ridiculously high levels
}