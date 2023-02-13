using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.Entities.Sun;

public record Sun : Entity<Sun, EntityState<SunAttributes>, SunAttributes>, IDisposable
{
    private IDisposable _subscribeDisposable;

    public Sun(IHaContext haContext, string entityId) : base(haContext, entityId)
    {
        Initialize();
    }

    public Sun(Entity entity) : base(entity)
    {
        Initialize();
    }

    public void Initialize()
    {
        _subscribeDisposable = StateAllChanges()
            .Subscribe(x =>
            {
                if (x.New == null)
                    return;

                OnStateChanged(new SunEventArgs(x));
            });
    }

    public event EventHandler<SunEventArgs>? StateChanged;

    protected void OnStateChanged(SunEventArgs e)
    {
        StateChanged?.Invoke(this, e);
    }

    public void Dispose()
    {
        _subscribeDisposable?.Dispose();
    }
}
