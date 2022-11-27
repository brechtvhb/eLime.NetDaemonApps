using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Tests.Mocks;

public interface IHaContextMock
{
    void TriggerStateChange(Entity entity, string newStateValue, object? attributes = null);
    void TriggerStateChange(string entityId, EntityState newState);
    void VerifyServiceCalled(Entity entity, string domain, string service);
}