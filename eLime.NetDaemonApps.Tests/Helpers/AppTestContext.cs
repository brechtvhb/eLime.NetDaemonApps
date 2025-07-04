﻿using eLime.NetDaemonApps.Domain.Entities.TextSensors;
using eLime.NetDaemonApps.Tests.Mocks.Moq;
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

    public void TriggerStateChange(Entity entity, string newStateValue)
    {
        HaContextMock.TriggerStateChange(entity, newStateValue);
    }
    public void TriggerStateChangeWithAttributes(Entity entity, string newStateValue, object attributes)
    {
        HaContextMock.TriggerStateChange(entity, newStateValue, attributes);
    }

    public void SimulateClick(StateSwitch @switch, string state = "single-press")
    {
        TriggerStateChange(@switch, state);
        TriggerStateChange(@switch, "none");
    }

    public void SimulateDoubleClick(StateSwitch @switch)
    {
        SimulateClick(@switch, "double-press");
    }
    public void SimulateTripleClick(StateSwitch @switch)
    {
        SimulateClick(@switch, "triple-press");
    }
    public void SimulateLongClick(StateSwitch @switch)
    {
        SimulateClick(@switch, "long-press");
    }
    public void SimulateUberLongClick(StateSwitch @switch)
    {
        SimulateClick(@switch, "uber-long-press");
    }

    public void TriggerStateChange(Entity entity, EntityState newState)
    {
        HaContextMock.TriggerStateChange(entity.EntityId, newState);
    }

    public void TriggerStateChange(string entityId, string newState)
    {
        HaContextMock.TriggerStateChange(entityId, newState);
    }

    public void TriggerStateChange(string entityId, EntityState oldState, EntityState newState)
    {
        HaContextMock.TriggerStateChange(entityId, oldState, newState);
    }
}