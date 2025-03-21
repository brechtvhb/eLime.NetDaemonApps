namespace eLime.NetDaemonApps.Domain.SmartHeatPump;

internal class SmartHeatPumpState
{
    internal SmartGridReadyMode SmartGridReadyMode { get; set; }
    internal double SourceTemperature { get; set; }
    internal DateTimeOffset? SourcePumpStartedAt { get; set; }
}