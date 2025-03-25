namespace eLime.NetDaemonApps.Domain.SmartHeatPump;

#pragma warning disable CS8618, CS9264
public class SmartGridReadyModeChangedEventArgs : EventArgs
{
    public required SmartGridReadyMode SmartGridReadyMode;

    public static SmartGridReadyModeChangedEventArgs Create(SmartGridReadyMode smartGridReadyMode) => new() { SmartGridReadyMode = smartGridReadyMode };
}