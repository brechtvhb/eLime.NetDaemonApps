using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace eLime.NetDaemonApps.Domain.Entities.BinarySensors;

public record EnabledSwitch : Entity<EnabledSwitch, EntityState<EnabledSwitchAttributes>, EnabledSwitchAttributes>
{
    public EnabledSwitch(IHaContext haContext, string entityId) : base(haContext, entityId)
    {
    }

    public EnabledSwitch(Entity entity) : base(entity)
    {
    }

    public void Initialize()
    {
        StateChanges()
            .Subscribe(x =>
            {
                if (x.New != null && x.New.IsOn())
                {
                    OnTurnedOn(new EnabledSwitchEventArgs(x));
                }
                if (x.New != null && x.New.IsOff())
                {
                    OnTurnedOff(new EnabledSwitchEventArgs(x));
                }
            });
    }

    public static EnabledSwitch Create(IHaContext haContext, string entityId)
    {
        var @switch = new EnabledSwitch(haContext, entityId);
        @switch.Initialize();
        return @switch;
    }

    public event EventHandler<EnabledSwitchEventArgs>? TurnedOn;
    public event EventHandler<EnabledSwitchEventArgs>? TurnedOff;

    protected void OnTurnedOn(EnabledSwitchEventArgs e)
    {
        TurnedOn?.Invoke(this, e);
    }
    protected void OnTurnedOff(EnabledSwitchEventArgs e)
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
}