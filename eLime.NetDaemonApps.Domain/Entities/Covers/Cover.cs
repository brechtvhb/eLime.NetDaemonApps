using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.Entities.Covers;

public record Cover : Entity<Cover, EntityState<CoverAttributes>, CoverAttributes>
{
    public Cover(IHaContext haContext, string entityId) : base(haContext, entityId)
    {
        Initialize();
    }

    public Cover(Entity entity) : base(entity)
    {
        Initialize();
    }


    public void Initialize()
    {
        StateChanges()
            .Subscribe(x =>
            {
                if (x.New == null)
                    return;

                if (x.New.IsOn())
                    OnOpened(new CoverEventArgs(x));
                else if (x.New.IsOff())
                    OnClosed(new CoverEventArgs(x));
                else
                    OnStateChanged(new CoverEventArgs(x));
            });
    }

    public void OpenCover()
    {
        CallService("open_cover");
    }

    public void CloseCover()
    {
        CallService("close_cover");
    }

    public event EventHandler<CoverEventArgs>? Opened;
    public event EventHandler<CoverEventArgs>? Closed;
    public event EventHandler<CoverEventArgs>? StateChanged;

    protected void OnOpened(CoverEventArgs e)
    {
        Opened?.Invoke(this, e);
    }
    protected void OnClosed(CoverEventArgs e)
    {
        Closed?.Invoke(this, e);
    }
    protected void OnStateChanged(CoverEventArgs e)
    {
        StateChanged?.Invoke(this, e);
    }

    public bool IsOpen() => State == "open";
    public bool IsClosed() => State == "closed";

}