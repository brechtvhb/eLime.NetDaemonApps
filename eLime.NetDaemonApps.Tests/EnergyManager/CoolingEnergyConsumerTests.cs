﻿using eLime.NetDaemonApps.Config.EnergyManager;
using eLime.NetDaemonApps.Domain.Storage;
using eLime.NetDaemonApps.Tests.EnergyManager.Builders;
using eLime.NetDaemonApps.Tests.Helpers;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace eLime.NetDaemonApps.Tests.EnergyManager;

[TestClass]
public class CoolingEnergyConsumerTests
{
    private AppTestContext _testCtx;
    private ILogger _logger;
    private IMqttEntityManager _mqttEntityManager;
    private IFileStorage _fileStorage;

    [TestInitialize]
    public void Init()
    {
        _testCtx = AppTestContext.Create(DateTime.Now);

        _logger = A.Fake<ILogger<Domain.EnergyManager.EnergyManager>>();
        _mqttEntityManager = A.Fake<IMqttEntityManager>();
        _fileStorage = A.Fake<IFileStorage>();
    }


    [TestMethod]
    public async Task Init_HappyFlow()
    {
        // Arrange
        var consumer = new CoolingEnergyConsumerBuilder()
            .WithLoad(-60, [LoadTimeFrames.Now, LoadTimeFrames.Last30Seconds], 200, [LoadTimeFrames.Now, LoadTimeFrames.Last30Seconds], 100)
            .Build();

        var energyManager = await new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        //Act

        //Assert
        Assert.AreEqual("Fridge", energyManager.Consumers.First().Name);
    }

    [TestMethod]
    public async Task HotFridge_TurnsOn()
    {
        // Arrange
        var consumer = new CoolingEnergyConsumerBuilder()
            .WithLoad(-60, [LoadTimeFrames.Now, LoadTimeFrames.Last30Seconds], 200, [LoadTimeFrames.Now, LoadTimeFrames.Last30Seconds], 100)
            .Build();
        _testCtx.TriggerStateChange(consumer.Cooling!.TemperatureSensor, "4");

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        _ = await builder.Build();
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "100");

        //Act
        _testCtx.TriggerStateChange(consumer.Cooling!.TemperatureSensor, "7");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Assert
        _testCtx.VerifySwitchTurnOn(consumer.Cooling!.SocketEntity, Moq.Times.Once);
    }

    [TestMethod]
    public async Task CoolFridge_TurnsOff()
    {
        // Arrange
        var consumer = new CoolingEnergyConsumerBuilder()
            .WithLoad(-60, [LoadTimeFrames.Now, LoadTimeFrames.Last30Seconds], 200, [LoadTimeFrames.Now, LoadTimeFrames.Last30Seconds], 100)
            .Build();

        _testCtx.TriggerStateChange(consumer.Cooling!.SocketEntity, "on");
        _testCtx.TriggerStateChange(consumer.Cooling!.TemperatureSensor, "4");

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        _ = await builder.Build();
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "100");

        //Act
        _testCtx.TriggerStateChange(consumer.Cooling!.TemperatureSensor, "0");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(5));

        //Assert
        _testCtx.VerifySwitchTurnOff(consumer.Cooling!.SocketEntity, Moq.Times.Once);
    }


    [TestMethod]
    public async Task VeryHotFridge_HasPriorityOver_HotFridge()
    {
        // Arrange
        var consumer1 = new CoolingEnergyConsumerBuilder()
            .WithLoad(-60, [LoadTimeFrames.Now, LoadTimeFrames.Last30Seconds], 200, [LoadTimeFrames.Now, LoadTimeFrames.Last30Seconds], 100)
            .Build();
        var consumer2 = new CoolingEnergyConsumerBuilder()
            .WithLoad(-60, [LoadTimeFrames.Now, LoadTimeFrames.Last30Seconds], 200, [LoadTimeFrames.Now, LoadTimeFrames.Last30Seconds], 100)
            .WithName("VeryHotFridge")
            .WithTemperatureSensor("sensor.very_hot_fridge_temperature", 1, 8)
            .Build();

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer1)
            .AddConsumer(consumer2);

        _ = await builder.Build();
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "100");


        //Act
        _testCtx.TriggerStateChange(consumer1.Cooling!.TemperatureSensor, "7");
        _testCtx.TriggerStateChange(consumer2.Cooling!.TemperatureSensor, "12");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(6));

        //Assert
        _testCtx.VerifySwitchTurnOn(consumer1.Cooling!.SocketEntity, Moq.Times.Never);
        _testCtx.VerifySwitchTurnOn(consumer2.Cooling!.SocketEntity, Moq.Times.Once);
    }

}