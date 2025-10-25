#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
namespace eLime.NetDaemonApps.Config;

public class SmartHeatPumpConfig
{
    public string SmartGridReadyInput1 { get; set; }
    public string SmartGridReadyInput2 { get; set; }
    public string SourcePumpRunningSensor { get; set; }
    public string SourceTemperatureSensor { get; set; }
    public string IsSummerModeSensor { get; set; }
    public string IsCoolingSensor { get; set; }
    public string StatusBytesSensor { get; set; }
    public string RemainingStandstillSensor { get; set; }
    public string HeatConsumedTodayIntegerSensor { get; set; }
    public string HeatConsumedTodayDecimalsSensor { get; set; }
    public string HeatProducedTodayIntegerSensor { get; set; }
    public string HeatProducedTodayDecimalsSensor { get; set; }
    public string HotWaterConsumedTodayIntegerSensor { get; set; }
    public string HotWaterConsumedTodayDecimalsSensor { get; set; }
    public string HotWaterProducedTodayIntegerSensor { get; set; }
    public string HotWaterProducedTodayDecimalsSensor { get; set; }

    public string IsgBaseUrl { get; set; }

    public SmartHeatPumpTemperatureConfig Temperatures { get; set; }
}

public class SmartHeatPumpTemperatureConfig
{
    public string RoomTemperatureSensor { get; set; }
    public decimal MinimumRoomTemperature { get; set; }
    public decimal ComfortRoomTemperature { get; set; }
    public decimal MaximumRoomTemperature { get; set; }

    public string HotWaterTemperatureSensor { get; set; }
    public decimal MinimumHotWaterTemperature { get; set; }
    public decimal ComfortHotWaterTemperature { get; set; }
    public decimal MaximumHotWaterTemperature { get; set; }


    public decimal TargetShowerTemperature { get; set; }
    public decimal TargetBathTemperature { get; set; }
}