﻿using eLime.NetDaemonApps.Tests.Mocks.Moq;
using Microsoft.Reactive.Testing;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

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

    public void TriggerStateChange(Entity entity, EntityState newState)
    {
        HaContextMock.TriggerStateChange(entity.EntityId, newState);
    }

    public void TriggerStateChange(string entityId, EntityState oldState, EntityState newState)
    {
        HaContextMock.TriggerStateChange(entityId, oldState, newState);
    }
}