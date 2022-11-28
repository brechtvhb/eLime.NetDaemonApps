using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;
using System.Reactive.Subjects;

namespace eLime.NetDaemonApps.Tests.Mocks;

public class HaContextMockBase : IHaContext
{
    public Dictionary<string, EntityState> _entityStates { get; } = new();
    public Subject<Event> EventsSubject { get; } = new();
    public Subject<StateChange> StateAllChangeSubject { get; } = new();

    public virtual void CallService(string domain, string service, ServiceTarget? target = null, object? data = null)
    {
    }

    public IObservable<Event> Events => EventsSubject;

    public IReadOnlyList<Entity> GetAllEntities() => _entityStates.Keys.Select(s => new Entity(this, s)).ToList();

    public Area? GetAreaFromEntityId(string entityId) => null;

    public EntityState? GetState(string entityId) => _entityStates.TryGetValue(entityId, out var result) ? result : null;

    public virtual void SendEvent(string eventType, object? data = null)
    {
    }

    public IObservable<StateChange> StateAllChanges() => StateAllChangeSubject;

    public void TriggerStateChange(Entity entity, string newStateValue, object? attributes = null)
    {
        var newState = new EntityState { State = newStateValue };
        if (attributes != null) newState = newState.WithAttributes(attributes);

        TriggerStateChange(entity.EntityId, newState);
    }

    public void TriggerStateChange(string entityId, EntityState newState)
    {
        var oldState = _entityStates.TryGetValue(entityId, out var current) ? current : null;
        _entityStates[entityId] = newState;
        StateAllChangeSubject.OnNext(new StateChange(new Entity(this, entityId), oldState, newState));
    }

    public virtual void VerifyServiceCalled(Entity entity, string domain, string service)
    {
    }

    public void TriggerEvent(Event @event)
    {
        EventsSubject.OnNext(@event);
    }

    public void TriggerStateChange(string entityId, EntityState oldState, EntityState newState)
    {
        _entityStates[entityId] = newState;
        StateAllChangeSubject.OnNext(new StateChange(new Entity(this, entityId), oldState, newState));
    }
}