using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.Entities.Select;

public record SelectEntity : Entity<SelectEntity, EntityState<SelectEntityAttribute>, SelectEntityAttribute>
{
    private IDisposable _subscribeDisposable;

    public SelectEntity(IHaContext haContext, string entityId) : base(haContext, entityId)
    {
    }

    public SelectEntity(Entity entity) : base(entity)
    {
    }

    public void Initialize()
    {
        _subscribeDisposable = StateChanges()
            .Subscribe(x =>
            {
                OnChanged(new SelectEntityEventArgs(x));
            });
    }

    public static SelectEntity Create(IHaContext haContext, string entityId)
    {
        var sensor = new SelectEntity(haContext, entityId);
        sensor.Initialize();
        return sensor;
    }

    public void Change(string value)
    {
        CallService("select_option", new SelectEntitySelectOptionParameters { Option = value });
    }

    public event EventHandler<SelectEntityEventArgs>? Changed;
    private void OnChanged(SelectEntityEventArgs e)
    {
        Changed?.Invoke(this, e);
    }

    public void Dispose()
    {
        _subscribeDisposable?.Dispose();
    }

}