using eLime.NetDaemonApps.Domain.Entities.ClimateEntities;
using eLime.NetDaemonApps.Domain.FlexiScenes.Rooms;
using eLime.NetDaemonApps.Domain.SmartVentilation;
using eLime.NetDaemonApps.Domain.Storage;
using eLime.NetDaemonApps.Tests.Builders;
using eLime.NetDaemonApps.Tests.Helpers;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;

namespace eLime.NetDaemonApps.Tests;

[TestClass]
public class SmartVentilationTests
{

    private AppTestContext _testCtx;
    private ILogger _logger;
    private IMqttEntityManager _mqttEntityManager;
    private IFileStorage _fileStorage;

    private Climate _climate;

    [TestInitialize]
    public void Init()
    {
        _testCtx = AppTestContext.Create(DateTime.Now);

        _logger = A.Fake<ILogger<Room>>();
        _mqttEntityManager = A.Fake<IMqttEntityManager>();
        _fileStorage = A.Fake<IFileStorage>();
        A.CallTo(() => _fileStorage.Get<VentilationFileStorage>("SmartVentilation", "SmartVentilation")).Returns(new VentilationFileStorage() { Enabled = true });

        _climate = new Climate(_testCtx.HaContext, "climate.comfod");
        _testCtx.TriggerStateChange(_climate, "fan_only");


    }

    [TestMethod]
    public void HappyFlow()
    {
        // Arrange
        _testCtx.TriggerStateChange(_climate, "_fan_only");

        //Act
        var ventilation = new VentilationBuilder(_testCtx, _logger, _mqttEntityManager, _fileStorage)
            .With(_climate)
            .Build();

        //Assert
        _testCtx.VerifyFanModeSet(_climate, VentilationState.Low, Moq.Times.Once);
    }
}