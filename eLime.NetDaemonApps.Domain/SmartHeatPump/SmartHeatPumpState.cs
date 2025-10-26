namespace eLime.NetDaemonApps.Domain.SmartHeatPump;

internal class SmartHeatPumpState
{
    public SmartGridReadyMode SmartGridReadyMode { get; set; }

    public HeatPumpEnergyDemand RoomEnergyDemand { get; set; }
    public HeatPumpEnergyDemand HotWaterEnergyDemand { get; set; }
    public HeatPumpEnergyDemand EnergyDemand { get; set; }

    public double SourceTemperature { get; set; }
    public DateTimeOffset? SourceTemperatureUpdatedAt { get; set; }
    public DateTimeOffset? SourcePumpStartedAt { get; set; }

    public decimal EcoRoomTemperature { get; set; }
    public decimal MaximumHotWaterTemperature { get; set; }

    public double? HeatCoefficientOfPerformance { get; set; }
    public double? HotWaterCoefficientOfPerformance { get; set; }

    public DateTimeOffset? ShowerRequestedAt { get; set; }
    public DateTimeOffset? BathRequestedAt { get; set; }
    public int ExpectedPowerConsumption { get; set; }
}


public enum HeatPumpEnergyDemand
{
    NoDemand,
    CanUse,
    Demanded,
    CriticalDemand
}