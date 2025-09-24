using eLime.NetDaemonApps.Config;
using eLime.NetDaemonApps.Domain.Entities.BinarySensors;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.Entities.TextSensors;
using eLime.NetDaemonApps.Domain.Storage;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.HassModel;
using System.Reactive.Concurrency;

namespace eLime.NetDaemonApps.Domain.SmartHeatPump;

public class SmartHeatPumpConfiguration
{
    public SmartHeatPumpContext Context { get; private init; }
    public BinarySwitch SmartGridReadyInput1 { get; private init; }
    public BinarySwitch SmartGridReadyInput2 { get; private init; }
    public BinarySensor SourcePumpRunningSensor { get; private init; }
    public NumericSensor SourceTemperatureSensor { get; private init; }

    public BinarySensor IsCoolingSensor { get; private init; }
    public TextSensor StatusBytesSensor { get; private init; }

    public NumericSensor HeatConsumedTodayIntegerSensor { get; private init; }
    public NumericSensor HeatConsumedTodayDecimalsSensor { get; private init; }
    public NumericSensor HeatProducedTodayIntegerSensor { get; private init; }
    public NumericSensor HeatProducedTodayDecimalsSensor { get; private init; }

    public NumericSensor HotWaterConsumedTodayIntegerSensor { get; private init; }
    public NumericSensor HotWaterConsumedTodayDecimalsSensor { get; private init; }
    public NumericSensor HotWaterProducedTodayIntegerSensor { get; private init; }
    public NumericSensor HotWaterProducedTodayDecimalsSensor { get; private init; }

    public SmartHeatPumpConfiguration(IHaContext haContext, ILogger logger, IScheduler scheduler, IFileStorage fileStorage, IMqttEntityManager mqttEntityManager, SmartHeatPumpConfig config, TimeSpan debounceDuration)
    {
        Context = new SmartHeatPumpContext(haContext, logger, scheduler, fileStorage, mqttEntityManager, debounceDuration);

        SmartGridReadyInput1 = BinarySwitch.Create(Context.HaContext, config.SmartGridReadyInput1);
        SmartGridReadyInput2 = BinarySwitch.Create(Context.HaContext, config.SmartGridReadyInput2);
        SourcePumpRunningSensor = BinarySensor.Create(Context.HaContext, config.SourcePumpRunningSensor);
        SourceTemperatureSensor = NumericSensor.Create(Context.HaContext, config.SourceTemperatureSensor);
        IsCoolingSensor = BinarySensor.Create(Context.HaContext, config.IsCoolingSensor);
        StatusBytesSensor = TextSensor.Create(Context.HaContext, config.StatusBytesSensor);
        HeatConsumedTodayIntegerSensor = NumericSensor.Create(Context.HaContext, config.HeatConsumedTodayIntegerSensor);
        HeatConsumedTodayDecimalsSensor = NumericSensor.Create(Context.HaContext, config.HeatConsumedTodayDecimalsSensor);
        HeatProducedTodayIntegerSensor = NumericSensor.Create(Context.HaContext, config.HeatProducedTodayIntegerSensor);
        HeatProducedTodayDecimalsSensor = NumericSensor.Create(Context.HaContext, config.HeatProducedTodayDecimalsSensor);
        HotWaterConsumedTodayIntegerSensor = NumericSensor.Create(Context.HaContext, config.HotWaterConsumedTodayIntegerSensor);
        HotWaterConsumedTodayDecimalsSensor = NumericSensor.Create(Context.HaContext, config.HotWaterConsumedTodayDecimalsSensor);
        HotWaterProducedTodayIntegerSensor = NumericSensor.Create(Context.HaContext, config.HotWaterProducedTodayIntegerSensor);
        HotWaterProducedTodayDecimalsSensor = NumericSensor.Create(Context.HaContext, config.HotWaterProducedTodayDecimalsSensor);
    }
}