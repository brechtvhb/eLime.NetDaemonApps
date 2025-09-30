namespace eLime.NetDaemonApps.Domain.SmartHeatPump;

#pragma warning disable CS8618, CS9264
internal class TemperatureSettings(SmartHeatPumpTemperatureConfiguration configuration)
{
    public decimal MinimumRoomTemperature { get; set; } = configuration.MinimumRoomTemperature;
    public decimal ComfortRoomTemperature { get; set; } = configuration.ComfortRoomTemperature;
    public decimal MaximumRoomTemperature { get; set; } = configuration.MaximumRoomTemperature;

    public decimal MinimumHotWaterTemperature { get; set; } = configuration.MinimumHotWaterTemperature;
    public decimal ComfortHotWaterTemperature { get; set; } = configuration.ComfortHotWaterTemperature;
    public decimal MaximumHotWaterTemperature { get; set; } = configuration.MaximumHotWaterTemperature;

    public decimal TargetShowerTemperature { get; set; } = configuration.TargetShowerTemperature;
    public decimal TargetBathTemperature { get; set; } = configuration.TargetBathTemperature;
}