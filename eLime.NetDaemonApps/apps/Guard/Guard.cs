using NetDaemon.Client;
using System.Threading;
using System.Threading.Tasks;

namespace eLime.NetDaemonApps.apps.Guard;


[NetDaemonApp(Id = "guard")]
public class Guard : IAsyncInitializable, IAsyncDisposable
{
    private readonly IHomeAssistantRunner _runner;

    public Guard(IHomeAssistantRunner runner)
    {
        _runner = runner;
    }

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        _runner.OnDisconnect.Subscribe(x => throw new Exception("Forcing container crash after home assistant connection got lost. Otherwise CPU somehow explodes after 4 hours"));

        return Task.CompletedTask;
    }



    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
