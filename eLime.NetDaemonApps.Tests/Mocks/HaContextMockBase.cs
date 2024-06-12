using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;
using System.Reactive.Subjects;
using System.Text.Json;

namespace eLime.NetDaemonApps.Tests.Mocks;

public class HaContextMockBase : IHaContext
{
    public Dictionary<string, EntityState> _entityStates { get; } = new();
    public Subject<Event> EventsSubject { get; } = new();
    public Subject<StateChange> StateAllChangeSubject { get; } = new();

    public virtual void CallService(string domain, string service, ServiceTarget? target = null, object? data = null)
    {
    }

    public Task<JsonElement?> CallServiceWithResponseAsync(string domain, string service, ServiceTarget? target = null, object? data = null)
    {
        throw new NotImplementedException();
    }

    public IObservable<Event> Events => EventsSubject;

    public IReadOnlyList<Entity> GetAllEntities() => _entityStates.Keys.Select(s => new Entity(this, s)).ToList();

    public Area? GetAreaFromEntityId(string entityId) => null;
    public EntityRegistration? GetEntityRegistration(string entityId)
    {
        throw new NotImplementedException();
    }

    public EntityState? GetState(string entityId) => _entityStates.TryGetValue(entityId, out var result) ? result : null;

    public virtual void SendEvent(string eventType, object? data = null)
    {
    }

    public IObservable<StateChange> StateAllChanges() => StateAllChangeSubject;

    public static JsonElement AsJsonElement(object value)
    {
        var jsonString = JsonSerializer.Serialize(value);
        return JsonSerializer.Deserialize<JsonElement>(jsonString);
    }

    public void TriggerStateChange(Entity entity, string newStateValue, object? attributes = null)
    {
        var newState = new EntityState { State = newStateValue, AttributesJson = attributes != null ? AsJsonElement(attributes) : null };
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