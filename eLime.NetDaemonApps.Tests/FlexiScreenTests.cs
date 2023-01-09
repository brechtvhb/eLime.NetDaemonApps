using eLime.NetDaemonApps.Domain.Entities.Covers;
using eLime.NetDaemonApps.Domain.Entities.Sun;
using eLime.NetDaemonApps.Domain.FlexiScenes.Rooms;
using eLime.NetDaemonApps.Domain.FlexiScreens;
using eLime.NetDaemonApps.Tests.Builders;
using eLime.NetDaemonApps.Tests.Helpers;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using NetDaemon.Extensions.MqttEntityManager;

namespace eLime.NetDaemonApps.Tests;

[TestClass]
public class FlexiScreenTests
{

    private AppTestContext _testCtx;
    private ILogger _logger;
    private IMqttEntityManager _mqttEntityManager;

    [TestInitialize]
    public void Init()
    {
        _testCtx = AppTestContext.Create(DateTime.Now);
        _testCtx.TriggerStateChange(new FlexiScreenEnabledSwitch(_testCtx.HaContext, "switch.flexiscreen_office"), "on");

        _logger = A.Fake<ILogger<Room>>();
        _mqttEntityManager = A.Fake<IMqttEntityManager>();
    }

    [TestMethod]
    public void Sun_Closes_Screen()
    {
        // Arrange
        var cover = new Cover(_testCtx.HaContext, "cover.office");
        _testCtx.TriggerStateChange(cover, "open");

        var sun = new Sun(_testCtx.HaContext, "sun.sun");
        var screen = new ScreenBuilder(_testCtx, _logger, _mqttEntityManager)
            .WithCover(cover)
            .WithSun(sun)
            .Build();

        //Act
        var sunAttributes = new SunAttributes
        {
            Azimuth = 240,
            Elevation = 20,
        };

        _testCtx.TriggerStateChange(sun, "above_horizon", sunAttributes);

        //Assert
        _testCtx.VerifyScreenGoesDown(cover, Moq.Times.Once);
    }
}