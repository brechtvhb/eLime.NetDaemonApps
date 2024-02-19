using eLime.NetDaemonApps.Domain.Entities.ClimateEntities;
using eLime.NetDaemonApps.Domain.Entities.NumericSensors;
using eLime.NetDaemonApps.Domain.FlexiScenes.Rooms;
using eLime.NetDaemonApps.Domain.SmartVentilation;
using eLime.NetDaemonApps.Domain.Storage;
using eLime.NetDaemonApps.Tests.Builders;
using eLime.NetDaemonApps.Tests.Helpers;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Tests;

[TestClass]
public class SmartVentilationTests
{

    private AppTestContext _testCtx;
    private ILogger _logger;
    private IMqttEntityManager _mqttEntityManager;
    private IFileStorage _fileStorage;

    private Climate _climate;
    private NumericSensor _co2Sensor;

    [TestInitialize]
    public void Init()
    {
        _testCtx = AppTestContext.Create(DateTime.Now);

        _logger = A.Fake<ILogger<Room>>();
        _mqttEntityManager = A.Fake<IMqttEntityManager>();
        _fileStorage = A.Fake<IFileStorage>();
        A.CallTo(() => _fileStorage.Get<VentilationFileStorage>("SmartVentilation", "SmartVentilation")).Returns(new VentilationFileStorage() { Enabled = true });

        _climate = new Climate(_testCtx.HaContext, "climate.comfod");
        _co2Sensor = new(_testCtx.HaContext, "sensor.co2");
    }

    [TestMethod]
    public void HappyFlow()
    {
        // Arrange
        _testCtx.TriggerStateChange(_climate, "fan_only");

        //Act
        var ventilation = new VentilationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage)
            .With(_climate)
            .Build();

        //Assert
        _testCtx.VerifyFanModeSet(_climate, VentilationState.Low, Moq.Times.Once);
    }

    [TestMethod]
    public void ManualChangePreventsPingPong()
    {
        // Arrange
        _testCtx.TriggerStateChange(_climate, "fan_only");
        var ventilation = new VentilationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage)
            .With(_climate)
            .WithStatePingPongGuard(TimeSpan.FromMinutes(30))
            .Build();

        _climate.SetFanMode("medium");

        //Act
        _testCtx.TriggerStateChange(_co2Sensor, new EntityState { State = "1500" });
        //Assert
        _testCtx.VerifyFanModeSet(_climate, VentilationState.Low, Moq.Times.Once);
    }

    [TestMethod]
    public void ManualChangeRevertsAfterCooldown()
    {
        // Arrange
        _testCtx.TriggerStateChange(_climate, "fan_only");
        var ventilation = new VentilationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage)
            .With(_climate)
            .WithStatePingPongGuard(TimeSpan.FromMinutes(30))
            .Build();

        _climate.SetFanMode("medium");
        _testCtx.TriggerStateChange(_co2Sensor, new EntityState { State = "300" });

        //Act
        _testCtx.AdvanceTimeBy(TimeSpan.FromMinutes(31));

        //Assert
        _testCtx.VerifyFanModeSet(_climate, VentilationState.Low, Moq.Times.Exactly(2));
    }
}