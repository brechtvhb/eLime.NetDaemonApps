using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.Entities.BinarySensors;

public record EnabledSwitch<T> : Entity<EnabledSwitch<T>, EntityState<T>, T>, IDisposable
    where T : EnabledSwitchAttributes
{
    private IDisposable _subscribeDisposable;

    public EnabledSwitch(IHaContext haContext, string entityId) : base(haContext, entityId)
    {
    }

    public EnabledSwitch(Entity entity) : base(entity)
    {
    }

    public void Initialize()
    {
        _subscribeDisposable = StateChanges()
            .Subscribe(x =>
            {
                if (x.New != null && x.New.IsOn())
                {
                    OnTurnedOn(new EnabledSwitchEventArgs<T>(x));
                }
                if (x.New != null && x.New.IsOff())
                {
                    OnTurnedOff(new EnabledSwitchEventArgs<T>(x));
                }
            });
    }

    public static EnabledSwitch<T> Create(IHaContext haContext, string entityId)
    {
        var @switch = new EnabledSwitch<T>(haContext, entityId);
        @switch.Initialize();
        return @switch;
    }

    public event EventHandler<EnabledSwitchEventArgs<T>>? TurnedOn;
    public event EventHandler<EnabledSwitchEventArgs<T>>? TurnedOff;

    protected void OnTurnedOn(EnabledSwitchEventArgs<T> e)
    {
        TurnedOn?.Invoke(this, e);
    }
    protected void OnTurnedOff(EnabledSwitchEventArgs<T> e)
    {
        TurnedOff?.Invoke(this, e);
    }

    ///<summary>Turn a switch on</summary>
    public void TurnOn()
    {
        CallService("turn_on");
    }

    ///<summary>Turn a switch off</summary>
    public void TurnOff()
    {
        CallService("turn_off");
    }

    public bool IsOn() => State == "on";

    public void Dispose()
    {
        _subscribeDisposable?.Dispose();
    }
}