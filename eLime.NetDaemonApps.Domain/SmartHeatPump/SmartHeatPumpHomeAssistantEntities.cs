using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.Entities.TextSensors;

namespace eLime.NetDaemonApps.Domain.SmartHeatPump;

#pragma warning disable CS8618, CS9264
public class SmartHeatPumpHomeAssistantEntities(SmartHeatPumpConfiguration config) : IDisposable
{
    internal BinarySwitch SmartGridReadyInput1 = config.SmartGridReadyInput1;
    internal BinarySwitch SmartGridReadyInput2 = config.SmartGridReadyInput2;
    internal BinarySensor SourcePumpRunningSensor = config.SourcePumpRunningSensor;
    internal NumericSensor SourceTemperatureSensor = config.SourceTemperatureSensor;
    internal TextSensor StatusBytesSensor = config.StatusBytesSensor;


    internal BinarySensor IsCoolingSensor = config.IsCoolingSensor;

    internal NumericSensor HeatConsumedTodayIntegerSensor = config.HeatConsumedTodayIntegerSensor;
    internal NumericSensor HeatConsumedTodayDecimalsSensor = config.HeatConsumedTodayDecimalsSensor;
    internal NumericSensor HeatProducedTodayIntegerSensor = config.HeatProducedTodayIntegerSensor;
    internal NumericSensor HeatProducedTodayDecimalsSensor = config.HeatProducedTodayDecimalsSensor;

    internal NumericSensor HotWaterConsumedTodayIntegerSensor = config.HotWaterConsumedTodayIntegerSensor;
    internal NumericSensor HotWaterConsumedTodayDecimalsSensor = config.HotWaterConsumedTodayDecimalsSensor;
    internal NumericSensor HotWaterProducedTodayIntegerSensor = config.HotWaterProducedTodayIntegerSensor;
    internal NumericSensor HotWaterProducedTodayDecimalsSensor = config.HotWaterProducedTodayDecimalsSensor;
    public void Dispose()
    {
        SmartGridReadyInput1.Dispose();
        SmartGridReadyInput2.Dispose();
        SourcePumpRunningSensor.Dispose();
        SourceTemperatureSensor.Dispose();
        StatusBytesSensor.Dispose();

        IsCoolingSensor.Dispose();

        HeatConsumedTodayIntegerSensor.Dispose();
        HeatConsumedTodayDecimalsSensor.Dispose();
        HeatProducedTodayIntegerSensor.Dispose();
        HeatProducedTodayDecimalsSensor.Dispose();
        HotWaterConsumedTodayIntegerSensor.Dispose();
        HotWaterConsumedTodayDecimalsSensor.Dispose();
        HotWaterProducedTodayIntegerSensor.Dispose();
        HotWaterProducedTodayDecimalsSensor.Dispose();
    }
}