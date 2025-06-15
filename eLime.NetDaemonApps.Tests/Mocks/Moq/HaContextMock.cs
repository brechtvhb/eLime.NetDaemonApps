using Moq;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;
using System.Reactive.Subjects;

namespace eLime.NetDaemonApps.Tests.Mocks.Moq;

public class HaContextMock : Mock<HaContextMockBase>
{
    public HaContextMock()
    {
        void Callback(string domain, string service, ServiceTarget target, object? data)
        {
            if (target != null)
                TriggerStateChange(target.EntityIds.First(), data == null ? new EntityState { State = "" } : new EntityState { State = data.ToString() });
        }

        Setup(m => m.CallService(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ServiceTarget>(), It.IsAny<object>()))
            .Callback(Callback)
            .CallBase();
        HaContext = Object;
    }

    public IHaContext HaContext { get; }

    public Subject<StateChange> StateChangeSubject { get; } = new();

    public void TriggerStateChange(Entity entity, string newStateValue, object? attributes = null)
    {
        Object.TriggerStateChange(entity, newStateValue, attributes);
    }

    public void TriggerStateChange(string entityId, string newState)
    {
        Object.TriggerStateChange(entityId, newState);
    }

    public void TriggerStateChange(string entityId, EntityState newState)
    {
        Object.TriggerStateChange(entityId, newState);
    }

    public void VerifyServiceCalled(Entity entity, string domain, string service)
    {
        Verify(m => m.CallService(domain, service,
            It.Is<ServiceTarget?>(s => s!.EntityIds!.SingleOrDefault() == entity.EntityId),
            null));
    }

    public void TriggerEvent(Event @event)
    {
        Object.TriggerEvent(@event);
    }

    public void TriggerStateChange(string entityId, EntityState oldState, EntityState newState)
    {
        Object.TriggerStateChange(entityId, oldState, newState);
    }
}