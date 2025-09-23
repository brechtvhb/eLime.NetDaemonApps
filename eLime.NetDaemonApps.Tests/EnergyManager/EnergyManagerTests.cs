using eLime.NetDaemonApps.Config.EnergyManager;
using eLime.NetDaemonApps.Domain.EnergyManager.Consumers;
using eLime.NetDaemonApps.Domain.Helper;
using eLime.NetDaemonApps.Domain.Storage;
using eLime.NetDaemonApps.Tests.EnergyManager.Builders;
using eLime.NetDaemonApps.Tests.Helpers;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace eLime.NetDaemonApps.Tests.EnergyManager;

//Things to test
//Load in correct state

[TestClass]
public class EnergyManagerTests
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
        var consumer = SimpleEnergyConsumerBuilder.PondPump().Build();

        var energyManager = await new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        //Act

        //Assert
        Assert.AreEqual("Pond pump", energyManager.Consumers.First().Name);
    }

    [TestMethod]
    public async Task Init_InRunningState()
    {
        // Arrange
        var startedAt = _testCtx.Scheduler.Now.AddMinutes(-5);
        A.CallTo(() => _fileStorage.Get<ConsumerState>("EnergyManager", "pond_pump")).Returns(new ConsumerState
        {
            State = EnergyConsumerState.Running,
            StartedAt = startedAt,
            LastRun = null,
        });

        var consumer = SimpleEnergyConsumerBuilder.PondPump().Build();

        _testCtx.TriggerStateChange(consumer.Simple!.SocketEntity, "on");
        _testCtx.TriggerStateChange(consumer.PowerUsageEntity, "40");

        //Act
        var energyManager = await new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        //Assert
        Assert.AreEqual(startedAt, energyManager.Consumers.First().State.StartedAt);
        Assert.AreEqual(true, energyManager.Consumers.First().IsRunning);
    }

    [TestMethod]
    public async Task Socket_Turning_on_Triggers_State_Running()
    {
        // Arrange
        var consumer = SimpleEnergyConsumerBuilder.PondPump().Build();

        var energyManager = await new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        //Act
        _testCtx.TriggerStateChange(consumer.Simple!.SocketEntity, "on");

        //Assert
        Assert.AreEqual(EnergyConsumerState.Running, energyManager.Consumers.First().State.State);
        Assert.AreEqual(_testCtx.Scheduler.Now, energyManager.Consumers.First().State.StartedAt);
        Assert.AreEqual(true, energyManager.Consumers.First().IsRunning);
        var mqttState = $"sensor.energy_consumer_{consumer.Name.MakeHaFriendly()}_state";
        A.CallTo(() => _mqttEntityManager.SetStateAsync(mqttState, EnergyConsumerState.Running.ToString())).MustHaveHappenedOnceExactly();
    }


    [TestMethod]
    public async Task Socket_Turning_off_Triggers_State_Off()
    {
        // Arrange
        var consumer = SimpleEnergyConsumerBuilder.PondPump().Build();

        _testCtx.TriggerStateChange(consumer.Simple!.SocketEntity, "on");
        _testCtx.TriggerStateChange(consumer.PowerUsageEntity, "40");

        var energyManager = await new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        //Act
        _testCtx.TriggerStateChange(consumer.Simple!.SocketEntity, "off");

        //Assert
        Assert.AreEqual(_testCtx.Scheduler.Now, energyManager.Consumers.First().State.LastRun);
        Assert.AreEqual(false, energyManager.Consumers.First().IsRunning);
        var mqttState = $"sensor.energy_consumer_{consumer.Name.MakeHaFriendly()}_state";
        A.CallTo(() => _mqttEntityManager.SetStateAsync(mqttState, EnergyConsumerState.NeedsEnergy.ToString())).MustHaveHappened(1, Times.Exactly);
    }

    [TestMethod]
    public async Task Exporting_Energy_SwitchesOnLoad()
    {
        // Arrange
        var consumer = SimpleEnergyConsumerBuilder.PondPump().Build();

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        _ = await builder.Build();

        //Act
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "2000");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(5));

        //Assert
        _testCtx.VerifySwitchTurnOn(consumer.Simple!.SocketEntity, Moq.Times.Once);
    }

    [TestMethod]
    public async Task Exporting_Energy_DoesNotSwitchOnLoad_BecauseNoSunIsExpected()
    {
        // Arrange
        var consumer = SimpleEnergyConsumerBuilder.PondPump()
            .WithLoad(-40, [LoadTimeFrames.SolarForecastNowCorrected], 100, [LoadTimeFrames.Now, LoadTimeFrames.Last30Seconds], 42)
            .Build();

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        _ = await builder.Build();

        //Act
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "2000");
        _testCtx.TriggerStateChange(builder._grid.CurrentSolarPowerEntity, "2000");
        _testCtx.TriggerStateChange(builder._grid.SolarForecastPowerNowEntity, "0");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(5));

        //Assert
        _testCtx.VerifySwitchTurnOn(consumer.Simple!.SocketEntity, Moq.Times.Never);
    }

    [TestMethod]
    public async Task Above_Peak_Energy_SwitchesOffLoad()
    {
        // Arrange
        var consumer = SimpleEnergyConsumerBuilder.PondPump().Build();

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        _ = await builder.Build();

        _testCtx.TriggerStateChange(consumer.Simple!.SocketEntity, "on");
        _testCtx.TriggerStateChange(consumer.PowerUsageEntity, "40");

        //Act
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "5000");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

        //Assert
        _testCtx.VerifySwitchTurnOff(consumer.Simple!.SocketEntity, Moq.Times.Once);
    }

    [TestMethod]
    public async Task Above_Peak_Energy_WithMinimumRuntime_SwitchesOffLoad_After_Duration()
    {
        // Arrange
        var consumer = SimpleEnergyConsumerBuilder.PondPump()
            .WithRuntime(TimeSpan.FromSeconds(55), TimeSpan.FromMinutes(60))
            .Build();

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        _ = await builder.Build();

        _testCtx.TriggerStateChange(consumer.Simple!.SocketEntity, "on");
        _testCtx.TriggerStateChange(consumer.PowerUsageEntity, "40");

        //Act
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "5000");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(61));

        //Assert
        _testCtx.VerifySwitchTurnOff(consumer.Simple!.SocketEntity, Moq.Times.Once);
    }

    [TestMethod]
    public async Task SwitchOffLoad_When_Consuming_More_Than_SwitchOffLoad_After_MinimumRuntime()
    {
        // Arrange
        var consumer = SimpleEnergyConsumerBuilder.PondPump()
            .WithRuntime(TimeSpan.FromSeconds(55), TimeSpan.FromMinutes(60))
            .WithLoad(-50, [LoadTimeFrames.Now, LoadTimeFrames.Last30Seconds], 200, [LoadTimeFrames.Now, LoadTimeFrames.Last30Seconds], 100)
            .Build();

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        _ = await builder.Build();

        _testCtx.TriggerStateChange(consumer.Simple!.SocketEntity, "on");
        _testCtx.TriggerStateChange(consumer.PowerUsageEntity, "40");

        //Act
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "500");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(61));

        //Assert
        _testCtx.VerifySwitchTurnOff(consumer.Simple!.SocketEntity, Moq.Times.Once);
    }

    [TestMethod]
    public async Task DoNotSwitchOffLoad_When_Consuming_More_Than_SwitchOffLoad_Before_MinimumRuntime()
    {
        // Arrange
        var consumer = SimpleEnergyConsumerBuilder.PondPump()
            .WithRuntime(TimeSpan.FromSeconds(90), TimeSpan.FromMinutes(60))
            .WithLoad(-50, [LoadTimeFrames.Now, LoadTimeFrames.Last30Seconds], 200, [LoadTimeFrames.Now, LoadTimeFrames.Last30Seconds], 100)
            .Build();

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        _ = await builder.Build();

        _testCtx.TriggerStateChange(consumer.Simple!.SocketEntity, "on");
        _testCtx.TriggerStateChange(consumer.PowerUsageEntity, "40");

        //Act
        _testCtx.TriggerStateChange(builder._grid.ImportEntity, "500");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(61));

        //Assert
        _testCtx.VerifySwitchTurnOff(consumer.Simple!.SocketEntity, Moq.Times.Never);
    }

    [TestMethod]
    public async Task MaximumRuntime_SwitchesOffLoad()
    {
        // Arrange
        var consumer = SimpleEnergyConsumerBuilder.PondPump()
            .WithRuntime(TimeSpan.FromSeconds(55), TimeSpan.FromSeconds(180))
            .Build();

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        _ = await builder.Build();

        _testCtx.TriggerStateChange(consumer.Simple!.SocketEntity, "on");
        _testCtx.TriggerStateChange(consumer.PowerUsageEntity, "40");

        //Act
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "500");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(181));

        //Assert
        _testCtx.VerifySwitchTurnOff(consumer.Simple!.SocketEntity, Moq.Times.Once);
    }


    [TestMethod]
    public async Task Init_SwitchOff_IfPastMaximumRuntime()
    {
        // Arrange
        var startedAt = _testCtx.Scheduler.Now.AddMinutes(-5);
        A.CallTo(() => _fileStorage.Get<ConsumerState>("EnergyManager", "pond_pump")).Returns(new ConsumerState
        {
            State = EnergyConsumerState.Running,
            StartedAt = startedAt,
            LastRun = null,
        });

        var consumer = SimpleEnergyConsumerBuilder.PondPump()
            .WithRuntime(TimeSpan.FromSeconds(55), TimeSpan.FromSeconds(180))
            .Build();

        _testCtx.TriggerStateChange(consumer.Simple!.SocketEntity, "on");
        _testCtx.TriggerStateChange(consumer.PowerUsageEntity, "40");

        //Act
        var energyManager = await new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer)
            .Build();

        //Assert
        Assert.AreEqual(false, energyManager.Consumers.First().IsRunning);
    }



    [TestMethod]
    public async Task Respects_Timeout()
    {
        // Arrange
        var consumer = SimpleEnergyConsumerBuilder.PondPump()
            .WithRuntime(TimeSpan.FromSeconds(55), TimeSpan.FromSeconds(180))
            .WithTimeout(TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(300))
            .Build();

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        _ = await builder.Build();

        _testCtx.TriggerStateChange(consumer.Simple!.SocketEntity, "on");
        _testCtx.TriggerStateChange(consumer.Simple!.SocketEntity, "off");

        //Act
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "1000");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(31));

        //Assert
        _testCtx.VerifySwitchTurnOn(consumer.Simple!.SocketEntity, Moq.Times.Never);
    }

    [TestMethod]
    public async Task Can_Turn_On_After_Timeout()
    {
        // Arrange
        var consumer = SimpleEnergyConsumerBuilder.PondPump()
            .WithRuntime(TimeSpan.FromSeconds(55), TimeSpan.FromSeconds(180))
            .WithTimeout(TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(300))
            .Build();

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        _ = await builder.Build();

        _testCtx.TriggerStateChange(consumer.Simple!.SocketEntity, "on");
        _testCtx.TriggerStateChange(consumer.Simple!.SocketEntity, "off");

        //Act
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "1000");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(20));

        //Assert
        _testCtx.VerifySwitchTurnOn(consumer.Simple!.SocketEntity, Moq.Times.Once);
    }

    [TestMethod]
    public async Task MultipleConsumers_SwitchOn_One_If_Not_Enough_Power()
    {
        // Arrange
        var consumer1 = new SimpleEnergyConsumerBuilder()
            .Build();

        var consumer2 = new SimpleEnergyConsumerBuilder()
            .WithLoad(-60, [LoadTimeFrames.Now, LoadTimeFrames.Last30Seconds], 200, [LoadTimeFrames.Now, LoadTimeFrames.Last30Seconds], 100)
            .WithName("fridge")
            .Build();

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer1)
            .AddConsumer(consumer2);
        _ = await builder.Build();

        //Act
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "50");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Assert
        _testCtx.VerifySwitchTurnOn(consumer1.Simple!.SocketEntity, Moq.Times.Once);
        _testCtx.VerifySwitchTurnOn(consumer2.Simple!.SocketEntity, Moq.Times.Never);
    }


    [TestMethod]
    public async Task MultipleConsumers_SwitchOn_Both_If_Enough_Power()
    {
        // Arrange
        var consumer1 = SimpleEnergyConsumerBuilder.PondPump()
            .Build();

        var consumer2 = SimpleEnergyConsumerBuilder.PondPump()
            .WithLoad(-60, [LoadTimeFrames.Now, LoadTimeFrames.Last30Seconds], 200, [LoadTimeFrames.Now, LoadTimeFrames.Last30Seconds], 100)
            .WithName("fridge")
            .Build();

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer1)
            .AddConsumer(consumer2);
        _ = await builder.Build();

        //Act
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "200");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Assert
        _testCtx.VerifySwitchTurnOn(consumer1.Simple!.SocketEntity, Moq.Times.Once);
        _testCtx.VerifySwitchTurnOn(consumer2.Simple!.SocketEntity, Moq.Times.Once);
    }

    [TestMethod]
    public async Task MultipleConsumers_Prioritizes_Critical()
    {
        // Arrange
        var consumer1 = SimpleEnergyConsumerBuilder.PondPump()
            .WithLoad(-60, [LoadTimeFrames.Now, LoadTimeFrames.Last30Seconds], 200, [LoadTimeFrames.Now, LoadTimeFrames.Last30Seconds], 100)
            .Build();

        var consumer2 = SimpleEnergyConsumerBuilder.PondPump()
            .WithLoad(-60, [LoadTimeFrames.Now, LoadTimeFrames.Last30Seconds], 200, [LoadTimeFrames.Now, LoadTimeFrames.Last30Seconds], 100)
            .WithName("fridge")
            .WithCriticalSensor("binary_sensor.fridge_too_hot")
            .Build();

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer1)
            .AddConsumer(consumer2);
        _ = await builder.Build();

        //Act
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "50");
        _testCtx.TriggerStateChange(consumer2.CriticallyNeededEntity, "on");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(5));

        //Assert
        _testCtx.VerifySwitchTurnOn(consumer1.Simple!.SocketEntity, Moq.Times.Never);
        _testCtx.VerifySwitchTurnOn(consumer2.Simple!.SocketEntity, Moq.Times.Once);
    }


    [TestMethod]
    public async Task Exporting_Energy_SwitchesOnLoad_If_Within_TimeWindow()
    {
        _testCtx.SetCurrentTime(DateTime.Today.AddDays(1).AddHours(10));

        // Arrange
        var consumer = SimpleEnergyConsumerBuilder.PondPump()
            .AddTimeWindow(null, TimeSpan.FromHours(8), TimeSpan.FromHours(12))
            .Build();

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        _ = await builder.Build();

        //Act
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "2000");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Assert
        _testCtx.VerifySwitchTurnOn(consumer.Simple!.SocketEntity, Moq.Times.Once);
    }

    [TestMethod]
    public async Task Exporting_Energy_SwitchesOnLoad_If_Within_Active_TimeWindow()
    {
        _testCtx.SetCurrentTime(DateTime.Today.AddDays(1).AddHours(10));

        // Arrange
        var consumer = SimpleEnergyConsumerBuilder.PondPump()
            .AddTimeWindow("binary_sensor.time_window_active", TimeSpan.FromHours(8), TimeSpan.FromHours(12))
            .Build();

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        _ = await builder.Build();

        //Act
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "2000");
        _testCtx.TriggerStateChange("binary_sensor.time_window_active", "on");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Assert
        _testCtx.VerifySwitchTurnOn(consumer.Simple!.SocketEntity, Moq.Times.Once);
    }


    [TestMethod]
    public async Task Exporting_Energy_DoesNotSwitchOnLoad_If_Outside_TimeWindow()
    {
        _testCtx.SetCurrentTime(DateTime.Today.AddDays(1).AddHours(13));

        // Arrange
        var consumer = SimpleEnergyConsumerBuilder.PondPump()
            .AddTimeWindow(null, TimeSpan.FromHours(8), TimeSpan.FromHours(12))
            .Build();

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        _ = await builder.Build();

        //Act
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "2000");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Assert
        _testCtx.VerifySwitchTurnOn(consumer.Simple!.SocketEntity, Moq.Times.Never);
    }

    [TestMethod]
    public async Task Exporting_Energy_DoesNotSwitchOnLoad_If_TimeWindow_Not_Active()
    {
        _testCtx.SetCurrentTime(DateTime.Today.AddDays(1).AddHours(10));

        // Arrange
        var consumer = SimpleEnergyConsumerBuilder.PondPump()
            .AddTimeWindow("binary_sensor.time_window_active", TimeSpan.FromHours(8), TimeSpan.FromHours(12))
            .Build();

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        _ = await builder.Build();

        //Act
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "2000");
        _testCtx.TriggerStateChange("binary_sensor.time_window_active", "off");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Assert
        _testCtx.VerifySwitchTurnOn(consumer.Simple!.SocketEntity, Moq.Times.Never);
    }

    [TestMethod]
    public async Task Exporting_Energy_SwitchesOnLoad_If_Within_A_TimeWindow()
    {
        _testCtx.SetCurrentTime(DateTime.Today.AddDays(1).AddHours(10));

        // Arrange
        var consumer = SimpleEnergyConsumerBuilder.PondPump()
            .AddTimeWindow(null, TimeSpan.FromHours(9), TimeSpan.FromHours(12))
            .AddTimeWindow(null, TimeSpan.FromHours(13), TimeSpan.FromHours(16))
            .Build();

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        _ = await builder.Build();

        //Act
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "2000");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Assert
        _testCtx.VerifySwitchTurnOn(consumer.Simple!.SocketEntity, Moq.Times.Once);
    }

    [TestMethod]
    public async Task Exporting_Energy_SwitchesOnLoad_If_Within_A_TimeWindow_With_Active_Entity()
    {
        _testCtx.SetCurrentTime(DateTime.Today.AddDays(1).AddHours(10));

        // Arrange
        var consumer = SimpleEnergyConsumerBuilder.PondPump()
            .AddTimeWindow("binary_sensor.away", TimeSpan.FromHours(9), TimeSpan.FromHours(12))
            .AddTimeWindow("binary_sensor.away", TimeSpan.FromHours(13), TimeSpan.FromHours(16))
            .Build();

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        _ = await builder.Build();

        //Act
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "2000");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Assert
        _testCtx.VerifySwitchTurnOn(consumer.Simple!.SocketEntity, Moq.Times.Once);
    }

    [TestMethod]
    public async Task Exporting_Energy_DoesNotSwitchOnLoad_If_Outside__All_TimeWindows()
    {
        _testCtx.SetCurrentTime(DateTime.Today.AddDays(1).AddHours(13));

        // Arrange
        var consumer = SimpleEnergyConsumerBuilder.PondPump()
            .AddTimeWindow(null, TimeSpan.FromHours(9), TimeSpan.FromHours(12))
            .AddTimeWindow(null, TimeSpan.FromHours(14), TimeSpan.FromHours(16))
            .Build();

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer);
        _ = await builder.Build();

        //Act
        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "2000");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(1));

        //Assert
        _testCtx.VerifySwitchTurnOn(consumer.Simple!.SocketEntity, Moq.Times.Never);
    }


    [TestMethod]
    public async Task MultipleConsumers_Does_Not_Start_Because_It_Corrected_Power_Consumption_Of_Consumer()
    {
        // Arrange
        var consumer1 = SimpleEnergyConsumerBuilder.PondPump()
            .WithLoad(-60, [LoadTimeFrames.LastMinute], 200, [LoadTimeFrames.LastMinute], 100)
            .Build();

        var consumer2 = SimpleEnergyConsumerBuilder.PondPump()
            .WithLoad(-60, [LoadTimeFrames.LastMinute], 200, [LoadTimeFrames.LastMinute], 2500)
            .WithName("fridge")
            .WithCriticalSensor("binary_sensor.fridge_too_hot")
            .Build();

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer1)
            .AddConsumer(consumer2);
        _ = await builder.Build();

        _testCtx.TriggerStateChange(consumer1.PowerUsageEntity, "0");
        _testCtx.TriggerStateChange(consumer2.PowerUsageEntity, "0");
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(1));

        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "100");
        _testCtx.TriggerStateChange(consumer2.CriticallyNeededEntity!, "on");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(5));
        _testCtx.TriggerStateChange(consumer2.Simple!.SocketEntity, "on");

        //Act
        _testCtx.TriggerStateChange(consumer2.PowerUsageEntity, "2500");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

        //Assert
        _testCtx.VerifySwitchTurnOn(consumer1.Simple!.SocketEntity, Moq.Times.Never);
        _testCtx.VerifySwitchTurnOn(consumer2.Simple!.SocketEntity, Moq.Times.Once);
    }

    [TestMethod]
    public async Task MultipleConsumers_Does_Start_If_Still_Exporting_And_Average_Passed()
    {
        // Arrange
        var consumer1 = SimpleEnergyConsumerBuilder.PondPump()
            .WithLoad(-60, [LoadTimeFrames.Last30Seconds], 200, [LoadTimeFrames.Last30Seconds], 100)
            .Build();

        var consumer2 = SimpleEnergyConsumerBuilder.PondPump()
            .WithLoad(-60, [LoadTimeFrames.Last30Seconds], 200, [LoadTimeFrames.Last30Seconds], 2500)
            .WithName("fridge")
            .WithCriticalSensor("binary_sensor.fridge_too_hot")
            .Build();

        var builder = new EnergyManagerBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage, _testCtx.Scheduler)
            .AddConsumer(consumer1)
            .AddConsumer(consumer2);
        _ = await builder.Build();

        _testCtx.TriggerStateChange(consumer1.PowerUsageEntity, "0");
        _testCtx.TriggerStateChange(consumer2.PowerUsageEntity, "0");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

        _testCtx.TriggerStateChange(builder._grid.ExportEntity, "100");
        _testCtx.TriggerStateChange(consumer2.CriticallyNeededEntity!, "on");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(5));
        _testCtx.TriggerStateChange(consumer2.Simple!.SocketEntity, "on");

        //Act
        _testCtx.TriggerStateChange(consumer2.PowerUsageEntity, "2500");
        _testCtx.AdvanceTimeBy(TimeSpan.FromSeconds(30));

        //Assert
        _testCtx.VerifySwitchTurnOn(consumer1.Simple!.SocketEntity, Moq.Times.Once);
        _testCtx.VerifySwitchTurnOn(consumer2.Simple!.SocketEntity, Moq.Times.Once);
    }
}