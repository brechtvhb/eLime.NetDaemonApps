namespace eLime.NetDaemonApps.Domain.SmartHeatPump;

public enum SmartGridReadyMode
{
    Blocked,
    Normal,
    Boosted, //Increases temperatures but does not overrule standstill?
    Maximized //Overrules standstill?
}