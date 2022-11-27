using NetDaemon.AppModel;

namespace eLime.NetDaemonApps.Tests.Helpers;

public class FakeAppConfig<T> : IAppConfig<T> where T : class, new()
{
    public FakeAppConfig(T instance)
    {
        Value = instance;
    }

    public T Value { get; }
}