using eLime.NetDaemonApps.Tests.Mocks.Moq;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;
using System.Reactive.Linq;

namespace eLime.NetDaemonApps.Tests;

public class App
{
    public App(IHaContext ha)
    {
        ha.StateChanges()
          .Where(n => n.Entity.EntityId == "some.entity" && n.New?.State == "on")
          .Subscribe(_ => { ha.CallService("domain", "service", ServiceTarget.FromEntity("some.entity")); });
    }
}

[TestClass]
public class SimpleTest
{
    [TestMethod]
    public void TestServiceCalled()
    {
        var haMock = new HaContextMock();

        var _ = new App(haMock.Object);
        haMock.TriggerStateChange("some.entity", new EntityState { EntityId = "some.entity", State = "on" });

        haMock.VerifyServiceCalled(new Entity(haMock.Object, "some.entity"), "domain", "service");
    }
}