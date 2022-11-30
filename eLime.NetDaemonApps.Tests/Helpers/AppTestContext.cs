using eLime.NetDaemonApps.Domain.BinarySensors;
using eLime.NetDaemonApps.Tests.Mocks.Moq;
using Microsoft.Reactive.Testing;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;
using Switch = eLime.NetDaemonApps.Domain.BinarySensors.Switch;

namespace eLime.NetDaemonApps.Tests.Helpers;

/// <summary>
///     Helper class to handle state of the test session
/// </summary>
public class AppTestContext
{
    public HaContextMock HaContextMock { get; } = new();
    public IHaContext HaContext => HaContextMock.HaContext;
    public TestScheduler Scheduler { get; } = new();

    public void AdvanceTimeBy(TimeSpan timeSpan)
    {
        Scheduler.AdvanceBy(timeSpan.Ticks);
    }

    public void AdvanceTimeTo(long absoluteTime)
    {
        Scheduler.AdvanceTo(absoluteTime);
    }

    public static AppTestContext Create(DateTime startTime)
    {
        var ctx = new AppTestContext();
        ctx.SetCurrentTime(startTime);
        return ctx;
    }

    public void SetCurrentTime(DateTime time)
    {
        AdvanceTimeTo(time.Ticks);
    }

    public void TriggerEvent(Event @event)
    {
        HaContextMock.TriggerEvent(@event);
    }

    public void TriggerStateChange(Entity entity, string newStateValue, object? attributes = null)
    {
        HaContextMock.TriggerStateChange(entity, newStateValue, attributes);
    }

    public void SimulateClick(Switch @switch, string type = "single")
    {
        TriggerStateChange(@switch, "on", new BinarySensorAttributes { Icon = type });
        TriggerStateChange(@switch, "off", new BinarySensorAttributes { Icon = type });
    }

    public void SimulateDoubleClick(Switch @switch)
    {
        SimulateClick(@switch, "double");
    }
    public void SimulateTripleClick(Switch @switch)
    {
        SimulateClick(@switch, "triple");
    }
    public void SimulateLongClick(Switch @switch)
    {
        SimulateClick(@switch, "long");
    }
    public void SimulateUberLongClick(Switch @switch)
    {
        SimulateClick(@switch, "uberLong");
    }

    public void TriggerStateChange(Entity entity, EntityState newState)
    {
        HaContextMock.TriggerStateChange(entity.EntityId, newState);
    }

    public void TriggerStateChange(string entityId, EntityState oldState, EntityState newState)
    {
        HaContextMock.TriggerStateChange(entityId, oldState, newState);
    }
}