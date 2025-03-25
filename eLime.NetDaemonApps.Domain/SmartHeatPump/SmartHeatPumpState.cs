namespace eLime.NetDaemonApps.Domain.SmartHeatPump;

internal class SmartHeatPumpState
{
    public SmartGridReadyMode SmartGridReadyMode { get; set; }
    public double SourceTemperature { get; set; }
    public DateTimeOffset? SourcePumpStartedAt { get; set; }

    public double? HeatCoefficientOfPerformance { get; set; }
    public double? HotWaterCoefficientOfPerformance { get; set; }
}