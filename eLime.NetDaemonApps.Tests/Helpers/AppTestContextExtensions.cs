using eLime.NetDaemonApps.Domain.BinarySensors;
using eLime.NetDaemonApps.Domain.Lights;
using eLime.NetDaemonApps.Domain.Scenes;
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


    public static void VerifyEventRaised(this AppTestContext ctx, string eventType, Func<Times> times, object? data = null)
    {
        ctx.HaContextMock.Verify(c => c.SendEvent(eventType, data), times);
    }

    public static void VerifyLightTurnOff(this AppTestContext ctx, Light entity, Func<Times> times)
    {
        ctx.HaContextMock.Verify(c => c.CallService("light", "turn_off", It.Is<ServiceTarget>(s => Match(entity.EntityId, s)), It.IsAny<LightTurnOffParameters>()), times);
    }

    public static void VerifyLightTurnOff(this AppTestContext ctx, Light entity, Times times)
    {
        ctx.HaContextMock.Verify(c => c.CallService("light", "light", It.Is<ServiceTarget>(s => Match(entity.EntityId, s)), It.IsAny<LightTurnOffParameters>()), times);
    }

    public static void VerifyLightTurnOn(this AppTestContext ctx, Light entity, Func<Times> times)
    {
        ctx.HaContextMock.Verify(c => c.CallService("light", "turn_on", It.Is<ServiceTarget>(s => Match(entity.EntityId, s)), It.IsAny<LightTurnOnParameters>()), times);
    }

    public static void VerifyLightTurnOn(this AppTestContext ctx, Light entity, Times times)
    {
        ctx.HaContextMock.Verify(c => c.CallService("light", "turn_on", It.Is<ServiceTarget>(s => Match(entity.EntityId, s)), It.IsAny<LightTurnOnParameters>()), times);
    }

    public static void VerifyLightTurnOn(this AppTestContext ctx, Light entity, LightTurnOnParameters parameters, Func<Times> times)
    {
        ctx.HaContextMock.Verify(c => c.CallService("light", "turn_on", It.Is<ServiceTarget>(s => Match(entity.EntityId, s)), parameters), times);
    }

    public static void VerifyLightTurnOn(this AppTestContext ctx, Light entity, LightTurnOnParameters parameters, Times times)
    {
        ctx.HaContextMock.Verify(c => c.CallService("light", "turn_on", It.Is<ServiceTarget>(s => Match(entity.EntityId, s)), parameters), times);
    }

    public static void VerifySwitchTurnOff(this AppTestContext ctx, BinarySwitch entity, Func<Times> times)
    {
        ctx.HaContextMock.Verify(c => c.CallService("switch", "turn_off", It.Is<ServiceTarget>(s => Match(entity.EntityId, s)), null), times);
    }

    public static void VerifySwitchTurnOn(this AppTestContext ctx, BinarySwitch entity, Func<Times> times)
    {
        ctx.HaContextMock.Verify(c => c.CallService("switch", "turn_on", It.Is<ServiceTarget>(s => Match(entity.EntityId, s)), null), times);
    }

    public static void VerifySceneTurnOn(this AppTestContext ctx, Scene entity, Func<Times> times)
    {
        ctx.HaContextMock.Verify(c => c.CallService("scene", "turn_on", It.Is<ServiceTarget>(s => Match(entity.SceneId, s)), It.IsAny<SceneTurnOnParameters>()), times);
    }

    public static void VerifySceneTurnOn(this AppTestContext ctx, Scene entity, SceneTurnOnParameters parameters, Func<Times> times)
    {
        ctx.HaContextMock.Verify(c => c.CallService("scene", "turn_on", It.Is<ServiceTarget>(s => Match(entity.SceneId, s)), parameters), times);
    }

    private static bool Match(string s, ServiceTarget x)
    {
        return x.EntityIds != null && x.EntityIds.Any(id => id == s);
    }
}