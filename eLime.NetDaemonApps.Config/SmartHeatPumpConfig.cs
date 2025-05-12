#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
namespace eLime.NetDaemonApps.Config;

public class SmartHeatPumpConfig
{
    public string SmartGridReadyInput1 { get; set; }
    public string SmartGridReadyInput2 { get; set; }
    public string SourcePumpRunningSensor { get; set; }
    public string SourceTemperatureSensor { get; set; }
    public string IsCoolingSensor { get; set; }
    public string StatusBytesSensor { get; set; }
    public string HeatConsumedTodayIntegerSensor { get; set; }
    public string HeatConsumedTodayDecimalsSensor { get; set; }
    public string HeatProducedTodayIntegerSensor { get; set; }
    public string HeatProducedTodayDecimalsSensor { get; set; }
    public string HotWaterConsumedTodayIntegerSensor { get; set; }
    public string HotWaterConsumedTodayDecimalsSensor { get; set; }
    public string HotWaterProducedTodayIntegerSensor { get; set; }
    public string HotWaterProducedTodayDecimalsSensor { get; set; }

}