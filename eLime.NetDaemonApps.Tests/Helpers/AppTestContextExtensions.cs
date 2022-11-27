﻿using eLime.NetDaemonApps.Domain.BinarySensors;
using eLime.NetDaemonApps.Domain.Lights;
using eLime.NetDaemonApps.Tests.Mocks.Moq;
using Moq;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Tests.Helpers;

public static class AppTestContextExtensions
{
    public static void ActivateScene(this AppTestContext ctx, string sceneName)
    {
        ctx.ChangeStateFor($"scene.{sceneName}")
           .FromState("off")
           .ToState("on");
    }

    public static IFromState ChangeStateFor(this AppTestContext ctx, string entityId) => new StateChangeContext(ctx, entityId);

    public static T? GetEntity<T>(this AppTestContext ctx, string entityId) where T : Entity => Activator.CreateInstance(typeof(T), ctx.HaContext, entityId) as T;

    public static T? GetEntity<T>(this HaContextMock ctx, string entityId) where T : Entity => Activator.CreateInstance(typeof(T), ctx.HaContext, entityId) as T;


    public static T? GetEntity<T>(this AppTestContext ctx, string entityId, string state) where T : Entity

    {
        var instance = Activator.CreateInstance(typeof(T), ctx.HaContext, entityId) as T;
        ctx.HaContextMock.TriggerStateChange(instance, state);
        return instance;
    }

    public static T? GetEntity<T>(this HaContextMock ctx, string entityId, string state) where T : Entity

    {
        var instance = Activator.CreateInstance(typeof(T), ctx.HaContext, entityId) as T;
        ctx.TriggerStateChange(instance, state);
        return instance;
    }

    public static void VerifyCallService(this AppTestContext ctx, string serviceCall, string entityId, Func<Times> times, object? data = null)
    {
        var domain = serviceCall[..serviceCall.IndexOf(".", StringComparison.InvariantCultureIgnoreCase)];
        var service = serviceCall[(serviceCall.IndexOf(".", StringComparison.InvariantCultureIgnoreCase) + 1)..];

        ctx.HaContextMock.Verify(
            c => c.CallService(domain, service, It.Is<ServiceTarget>(s => Match(entityId, s)), data),
            times
        );
    }

    public static void VerifyCallService(this AppTestContext ctx, string serviceCall, string entityId, Times times, object? data = null)
    {
        var domain = serviceCall[..serviceCall.IndexOf(".", StringComparison.InvariantCultureIgnoreCase)];
        var service = serviceCall[(serviceCall.IndexOf(".", StringComparison.InvariantCultureIgnoreCase) + 1)..];

        ctx.HaContextMock.Verify(
            c => c.CallService(domain, service, It.Is<ServiceTarget>(s => Match(entityId, s)), data),
            times
        );
    }

    public static void VerifyEventRaised(this AppTestContext ctx, string eventType, Func<Times> times, object? data = null)
    {
        ctx.HaContextMock.Verify(c => c.SendEvent(eventType, data), times);
    }

    public static void VerifyLightTurnOff(this AppTestContext ctx, Light entity, Func<Times> times)
    {
        ctx.VerifyCallService("light.turn_off", entity.EntityId, times, new LightTurnOffParameters());
    }

    public static void VerifyLightTurnOff(this AppTestContext ctx, Light entity, Times times)
    {
        ctx.VerifyCallService("light.turn_off", entity.EntityId, times, new LightTurnOffParameters());
    }

    public static void VerifyLightTurnOn(this AppTestContext ctx, Light entity, Func<Times> times)
    {
        ctx.VerifyCallService("light.turn_on", entity.EntityId, times, new LightTurnOnParameters());
    }

    public static void VerifyLightTurnOn(this AppTestContext ctx, Light entity, Times times)
    {
        ctx.VerifyCallService("light.turn_on", entity.EntityId, times, new LightTurnOnParameters());
    }

    public static void VerifySwitchTurnOff(this AppTestContext ctx, Switch entity, Func<Times> times)
    {
        ctx.VerifyCallService("switch.turn_off", entity.EntityId, times);
    }

    public static void VerifySwitchTurnOn(this AppTestContext ctx, Switch entity, Func<Times> times)
    {
        ctx.VerifyCallService("switch.turn_on", entity.EntityId, times);
    }

    public static IWithState WithEntityState<T>(this AppTestContext ctx, string entityId, T state)
    {
        var stateChangeContext = new StateChangeContext(ctx, entityId);
        stateChangeContext.WithEntityState(entityId, state);
        return stateChangeContext;
    }

    private static bool Match(string s, ServiceTarget x)
    {
        return x.EntityIds != null && x.EntityIds.Any(id => id == s);
    }
}